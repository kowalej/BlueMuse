param([string]$pw = "")

$certInfo = New-SelfSignedCertificate -Type Custom -Subject "CN=BlueMuse - Jason Kowaleski" -KeyUsage DigitalSignature -FriendlyName "BlueMuse - Jason Kowaleski" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(1)
$thumbPrint = $certInfo.Thumbprint
$pwSecure = ConvertTo-SecureString -String $pw -Force -AsPlainText 
Export-PfxCertificate -cert "Cert:\LocalMachine\My\$thumbPrint" -FilePath BlueMuse_PackagingKey.pfx -Password $pwSecure