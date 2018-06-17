$package = Get-AppxPackage -name '07220b98-ffa5-4000-9f7c-e168a00899a6'
if($package){
	Remove-AppxPackage $package.PackageFullName;
}