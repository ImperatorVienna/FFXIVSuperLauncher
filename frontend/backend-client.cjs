'use strict';
const { spawn } = require('node:child_process');
const {contracts} = require('./ipc-contract.cjs');

// Used only in Electron's main process. Each call has a deadline and a bounded response.
class BackendClient {
  constructor(executable, args, { timeout = 10000, spawnProcess = spawn, waitForGame = false } = {}) {
    this.waitForGame = waitForGame;
    this.pending = new Map();
    this.sequence = 0;
    this.timeout = timeout;
    this.closed = false;
    this.buffer = '';
    this.child = spawnProcess(executable, args, { stdio: ['pipe','pipe','pipe'], shell: false });
    this.child.stdout.setEncoding('utf8');
    this.child.stdout.on('data', chunk => this.receive(chunk));
    // Never forward raw child-process diagnostics or potential secrets into the renderer.
    this.child.stderr.resume();
    this.child.on('error', () => this.fail('backend-unavailable'));
    this.child.on('exit', () => {this.exited=true;this.fail('backend-exited');});
    this.child.stdin.on('error', () => this.fail('backend-unavailable'));
  }
  request(method, params = {}) {
    if (this.closed) return Promise.reject(new Error('backend-unavailable'));
    if (!Object.hasOwn(contracts,method)) return Promise.reject(new Error('unknown-method'));
    if (!contracts[method](params)) return Promise.reject(new Error('invalid-request'));
    if (this.pending.size >= 16) return Promise.reject(new Error('backend-busy'));
    const id = String(++this.sequence);
    const payload = JSON.stringify({ id, method, params });
    if (Buffer.byteLength(payload) > 65536) return Promise.reject(new Error('invalid-request'));
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        // These reads run outside the host command loop and cannot mutate settings.
        if (method === 'compatibility.read' || method === 'areas.list') {
          this.pending.delete(id);reject(new Error('read-timeout'));return;
        }
        // A stalled sequential host cannot safely accept another settings mutation.
        this.fail('backend-timeout');
        this.child.kill();
      }, this.timeout);
      this.pending.set(id, { resolve, reject, timer });
      try { this.child.stdin.write(payload + '\n'); }
      catch { this.fail('backend-unavailable'); this.child.kill(); }
    });
  }
  receive(chunk) {
    if (this.closed) return;
    this.buffer += chunk;
    if (Buffer.byteLength(this.buffer) > 1048576) { this.fail('invalid-response'); this.child.kill(); return; }
    for (let end; (end = this.buffer.indexOf('\n')) >= 0;) {
      const line = this.buffer.slice(0,end); this.buffer = this.buffer.slice(end+1);
      let response;
      try { response = JSON.parse(line); } catch { this.fail('invalid-response'); this.child.kill(); return; }
      if (!response || typeof response.id !== 'string') { this.fail('invalid-response'); this.child.kill(); return; }
      const waiting = this.pending.get(response.id);
      if (!waiting) continue;
      clearTimeout(waiting.timer); this.pending.delete(response.id);
      if (response.error) {
        const error = new Error(response.error.code || 'operation-failed');
        error.logMessage = typeof response.error.logMessage === 'string' ? response.error.logMessage.slice(0,1000) : '';
        error.userMessage = typeof response.error.message === 'string' ? response.error.message.slice(0,1000) : '';
        waiting.reject(error);
      }
      else if (!Object.hasOwn(response, 'result')) waiting.reject(new Error('invalid-response'));
      else waiting.resolve(response.result);
    }
  }
  fail(code) {
    const notify=!this.closed&&code!=='backend-closed';
    this.closed = true;
    for (const { reject, timer } of this.pending.values()) { clearTimeout(timer); reject(new Error(code)); }
    this.pending.clear();
    if(notify)try{this.onFailure?.(code);}catch{}
  }
  close() {
    if(this.closing)return this.closing;
    this.fail('backend-closed');
    if(this.exited)return Promise.resolve();
    return this.closing=new Promise(resolve=>{
      const timer=this.waitForGame ? null : setTimeout(()=>{this.child.kill('SIGKILL');resolve();},8000);
      this.child.once('exit',()=>{clearTimeout(timer);resolve();});
      // EOF lets the host cancel authentication, close its verification browser and release locks.
      try{this.child.stdin.end();}catch{this.child.kill();}
    });
  }
}
module.exports = { BackendClient };
