$sizes = @(32, 128, 512, 1024, 2048, 4096, 8192);

foreach ($size in $sizes)
{
    dotnet run --framework netcoreapp2.1 -c Release $size
}