param([string]$pw = "", [int]$expiryYears = 2)

$certInfo = New-SelfSignedCertificate -Type Custom -Subject "CN=BlueMuse - Jason Kowaleski, O=NeuroTechX, C=CA" -KeyUsage DigitalSignature -FriendlyName "BlueMuse - Jason Kowaleski" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears($expiryYears) -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$thumbPrint = $certInfo.Thumbprint
$pwSecure = ConvertTo-SecureString -String $pw -Force -AsPlainText 
Export-PfxCertificate -cert "Cert:\LocalMachine\My\$thumbPrint" -FilePath BlueMuse_PackagingKey.pfx -Password $pwSecure