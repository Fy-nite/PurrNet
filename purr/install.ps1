param(
    [string]$version = "1.2.2"
)

# build and produce nupkg (will appear in ./nupkgs)
dotnet pack -c Release 
# remove previous global install if exists
dotnet tool uninstall --global purr
# install from local folder (tool manifest not required for global install)
dotnet tool install --global --add-source ./bin/Release --version $version purr
