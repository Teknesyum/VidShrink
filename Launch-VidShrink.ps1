$project = Join-Path $PSScriptRoot "src\VidShrink.App\VidShrink.App.csproj"
& dotnet run --project $project --configuration Debug --nologo
