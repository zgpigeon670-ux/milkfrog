param([string]$Output = 'Builds/PCGValidation/Offline')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$target = [IO.Path]::GetFullPath((Join-Path $project $Output))
if (!$target.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside the project.' }
New-Item -ItemType Directory -Force -Path $target | Out-Null
$configSource = [Security.SecurityElement]::Escape((Join-Path $project 'Assets\CombatDemo\Runtime\PCG\WfcConfig.cs'))
$solverSource = [Security.SecurityElement]::Escape((Join-Path $project 'Assets\CombatDemo\Runtime\PCG\WfcSolver.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$configSource"/><Compile Include="$solverSource"/><Compile Include="Harness.cs"/></ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $target 'PcgOffline.csproj') -Encoding UTF8
@'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Milkfrog.CombatDemo.PCG;
namespace UnityEngine {
    public class ScriptableObject { }
    public class RangeAttribute : Attribute { public RangeAttribute(float a,float b){} }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
}
class Harness {
    static int checks;
    static void Check(bool value,string label) { if(!value)throw new Exception(label); checks++; }
    static void Main(string[] args) {
        var c=new WfcConfig();
        Check(WfcConfig.Rotate(1)==2&&WfcConfig.Rotate(8)==1,"Rotation");
        Check(c.modules.Count==31&&c.modules.Select(m=>m.room+":"+m.ports).Distinct().Count()==31,"Deduplicated modules");
        var entropy=new WfcSolver(c,18);
        Check(Math.Abs(entropy.Entropy(1))<1e-8&&entropy.Entropy(3)>0,"Weighted entropy");
        var a=new WfcSolver(c,-190).Solve();var b=new WfcSolver(c,-190).Solve();
        Check(a.success&&b.success&&a.tiles.SequenceEqual(b.tiles),"Determinism");
        var bad=new WfcConfig();bad.modules[0].weight=float.NaN;var invalid=new WfcSolver(bad,0).Solve();
        Check(!invalid.success&&invalid.stats.attempts==0,"Invalid weight rejected");
        bad=new WfcConfig();bad.modules=new List<WfcModule>{new WfcModule{id="Cross",room=true,ports=15}};bad.maxAttempts=2;bad.maxBacktracks=0;
        invalid=new WfcSolver(bad,0).Solve();Check(!invalid.success&&invalid.stats.attempts==2&&!string.IsNullOrEmpty(invalid.error),"Impossible tileset terminates");
        var trace=new WfcSolver(c,18);int steps=0;
        foreach(var step in trace.Steps()){Check(step.domains.Length==100,"Trace domain count");if(step.kind==WfcStepKind.Complete)Check(step.domains.All(d=>WfcSolver.Bits(d)==1),"All collapsed");steps++;}
        Check(trace.Result.success&&steps>3,"Trace produces accepted map");
        var csv=new StringBuilder("mode,seed,success,localSolutions,attempts,backtracks,collapses,propagations,milliseconds,walkable,rooms,reachable,shortestCells,loops,error\n");
        var outcomes=new List<object>();int hybridPassed=0,baselinePassed=0,totalBacktracks=0;
        for(int mode=0;mode<2;mode++)for(int seed=0;seed<100;seed++){
            var r=new WfcSolver(c,seed,mode==0).Solve();var s=r.stats;
            if(r.success){if(mode==0)hybridPassed++;else baselinePassed++;}
            if(mode==0){Check(r.success,"Hybrid seed "+seed+": "+r.error);Check(WfcSolver.Inspect(c,r.tiles,r.start,r.goal).success,"Independent inspection");totalBacktracks+=s.backtracks;}
            Check(s.attempts<=c.maxAttempts&&s.backtracks<=c.maxBacktracks*c.maxAttempts,"Budget bounded");
            csv.AppendLine(string.Join(",",mode==0?"hybrid":"unconstrained",seed,r.success?1:0,s.localSolutions,s.attempts,s.backtracks,s.collapses,s.propagations,s.milliseconds.ToString("F3",CultureInfo.InvariantCulture),s.walkable,s.rooms,s.reachable,s.shortestPath,s.loops,"\""+(r.error??"").Replace("\"","\"\"")+"\""));
            if(seed==18 || mode==1 && !r.success && seed<3)outcomes.Add(new{mode=mode==0?"hybrid":"unconstrained",seed,r.success,r.error,r.start,r.goal,r.tiles,r.encounters});
            if(seed%10==0)Console.WriteLine($"mode={mode} seed={seed} success={r.success} backtracks={s.backtracks} ms={s.milliseconds:F1}");
        }
        Check(totalBacktracks>0,"Backtracking exercised");
        Directory.CreateDirectory(args[0]);
        File.WriteAllText(Path.Combine(args[0],"benchmark-offline.csv"),csv.ToString(),new UTF8Encoding(false));
        var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
        File.WriteAllText(Path.Combine(args[0],"maps-offline.json"),System.Text.Json.JsonSerializer.Serialize(outcomes,options));
        File.WriteAllText(Path.Combine(args[0],"offline-checks.json"),System.Text.Json.JsonSerializer.Serialize(new{runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,note="Actual WfcConfig/WfcSolver source; Unity attribute/ScriptableObject shell only. No Unity physics or gameplay executed.",checks,hybridPassed,baselinePassed,totalBacktracks},options));
        Console.WriteLine($"PASS {checks} checks; hybrid={hybridPassed}/100 baseline={baselinePassed}/100");
    }
}
'@ | Set-Content -LiteralPath (Join-Path $target 'Harness.cs') -Encoding UTF8
$env:DOTNET_CLI_HOME = $target
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet run --project (Join-Path $target 'PcgOffline.csproj') --configuration Release -- (Join-Path $project 'Assets\CombatDemo\Validation\PCG')
if ($LASTEXITCODE -ne 0) { throw 'Offline PCG checks failed.' }
