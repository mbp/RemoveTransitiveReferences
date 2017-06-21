# Remove not needed transitive references from csproj files

If you are using the new project format introduced with Visual Studio 2017 (look for `<Project Sdk="Microsoft.NET.Sdk"`>),
then transitive references are automatically included.

It is therefor not required to have it as a separate `<PackageReference ..>` in csproj, if another package references it.

This project finds and discovers transitive references that can be removed from csproj.

## Usage

Use directly on a csproj:

    RemoveTransitiveReferenes.exe [filename]

Or on a directory:

    RemoveTransitiveReferenes.exe [directory]