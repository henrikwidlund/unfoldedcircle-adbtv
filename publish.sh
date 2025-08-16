#!/bin/bash

# Remove old unfolded-circle-adbtv.tar.gz if it exists
rm -f ./unfolded-circle-adbtv.tar.gz

# Remove old publish directory if it exists
rm -rf ./publish

# Clean build and publish directories using dotnet clean
echo "Clean"
dotnet clean -c Release -p:BuildForLinuxArm=true

# Run dotnet publish
echo "Publish"
dotnet publish ./src/UnfoldedCircle.AdbTv/UnfoldedCircle.AdbTv.csproj -c Release -p:BuildForLinuxArm=true -o ./publish

# Enter the publish directory
cd ./publish || exit

# Create a new directory called driver
mkdir -p driverdir

# Create bin, config, and data folders in the driver directory
mkdir -p ./driverdir/bin ./driverdir/config ./driverdir/data

# Modify driver.json with the current date and version
date=$(date -u +"%Y-%m-%d")
unprefixed_version=$(date -u +"%Y.%m.%d")
jq --arg version "$unprefixed_version" --arg date "$date" \
  '.version = $version | .release_date = $date' \
  ./driver.json > tmp.json && mv tmp.json ./driver.json

# Copy driver.json to the root of the driver directory
cp ./driver.json ./driverdir/

# Copy appsettings*.json to the bin directory
cp ./appsettings*.json ./driverdir/bin/

# Copy driver (file) and *.pdb files from the publish directory to the bin directory in the driver directory
cp ./driver ./driverdir/bin/
cp ./*.pdb ./driverdir/bin/

# Download the latest version of adb for arm64 and copy it to the bin directory
curl -sL "$(curl -s https://api.github.com/repos/prife/adb/releases/latest | grep browser_download_url | cut -d\" -f4 | grep 'aarch64-29.0.6-optimize$')" -o adb
chmod a+x adb
mv adb driverdir/bin/

# Package the driver directory into a tarball
cd ./driverdir || exit
tar -czvf ../../unfolded-circle-adbtv.tar.gz ./*

# Remove the output directory
rm -rf ../../publish