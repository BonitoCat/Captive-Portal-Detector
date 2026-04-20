#! /bin/bash

CONTROL_FILE_X64="./cpd-linux-x64/DEBIAN/control"

VERSION=$(sed -n 's/^Version: //p' "$CONTROL_FILE_X64")
echo "Current version: $VERSION"

read -p "Enter new version number: " NEW_VERSION

if [ -f "$CONTROL_FILE_X64" ]; then
    echo "Changing version in $CONTROL_FILE_X64"
    sed -i "s/^Version: .*/Version: $NEW_VERSION/" "$CONTROL_FILE_X64"
fi

dotnet publish ../CaptivePortalTray.csproj \
    -c Release -r linux-x64 -f net8.0 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    --output ./cpd-linux-x64/opt/"Captive Portal Detector" \

dpkg-deb --build cpd-linux-x64 ./output/cpd-linux-x64.deb

echo "Press any key to exit..."
read _

