using Mono.Cecil;
using Mono.Cecil.Cil;
if(args.Length!=2 || Path.GetFullPath(args[0])==Path.GetFullPath(args[1]) || File.Exists(args[1]))
    throw new ArgumentException("Use a new output file; never overwrite the installed injector.");
using var a=AssemblyDefinition.ReadAssembly(args[0]);
var methods=a.MainModule.Types.SelectMany(Flatten).SelectMany(t=>t.Methods).Where(m=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.FullName=="FfxivArgLauncher.ArgFixer" && r.Name=="Fix")).ToArray();
if(methods.Length!=1)throw new Exception("Unexpected patch target count");
var m=methods[0];var ins=m.Body.Instructions;
var expected=new[]{Code.Ldarg_1,Code.Ldc_I4_1,Code.Newobj,Code.Stloc_0,Code.Ldloc_0,Code.Callvirt};
if(!ins.Take(6).Select(i=>i.OpCode.Code).SequenceEqual(expected))throw new Exception("Unexpected IL");
if(((MethodReference)ins[2].Operand).DeclaringType.FullName!="FfxivArgLauncher.ArgFixer")throw new Exception("Unexpected constructor");
foreach(var i in ins.Take(6)){i.OpCode=OpCodes.Nop;i.Operand=null;}
a.Write(args[1]);Console.WriteLine("Verified copy: bypassed only ArgFixer construction/Fix; entrypoint rewrite unchanged.");
static IEnumerable<TypeDefinition> Flatten(TypeDefinition t) {yield return t;foreach(var n in t.NestedTypes.SelectMany(Flatten))yield return n;}
