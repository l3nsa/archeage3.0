#!/bin/bash
echo "Building AAEmu ArcheAge 3.0 Server..."
dotnet restore
dotnet build --configuration Release
echo "Build complete!"
