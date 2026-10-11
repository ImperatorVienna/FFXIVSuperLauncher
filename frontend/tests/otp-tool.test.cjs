'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const {webcrypto}=require('node:crypto');
const {localOtp}=require('../../prototypes/electron-ui/otp-tool.js');
test('standalone OTP generator follows RFC 6238 SHA-1 vectors with six digits',async()=>{
  const secret='GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ';
  for(const [seconds,expected] of [[59,'287082'],[1111111109,'081804'],[1111111111,'050471'],[1234567890,'005924'],[2000000000,'279037'],[20000000000,'353130']])
    assert.equal(await localOtp(secret,seconds,webcrypto.subtle),expected);
  await assert.rejects(localOtp('123456',59,webcrypto.subtle));await assert.rejects(localOtp('GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQR',59,webcrypto.subtle));
});
