# Lance un serveur Archipelago sur une seed générée.
# usage : .\tools\serveur.ps1 v1\output\cmr_solo_42.zip [-Password motdepasse] [-Local]
#   -Password : mot de passe demandé aux joueurs (à mettre dans le champ « Mot de passe » de « Se connecter »).
#               Sans lui, n'importe qui qui peut joindre le port 38281 peut se connecter à un slot.
#   -Local    : n'écoute que sur cette machine (localhost) ; pour une partie avec un ami à distance, ne pas le mettre.
# Dans la fenêtre du serveur, tu peux taper par exemple :
#   /send Nelem Up Dash        (donne un item)
param(
    [Parameter(Mandatory = $true)][string]$Seed,
    [string]$Password = "",
    [switch]$Local
)

$root = Split-Path -Parent $PSScriptRoot
$seedPath = (Resolve-Path $Seed).Path
$python = Join-Path $root "tools\venv\Scripts\python.exe"
$serverArgs = @("MultiServer.py", "--port", "38281")
if ($Local) {
    $serverArgs += @("--host", "localhost")
    Write-Host "Serveur limité à cette machine (localhost:38281)."
} else {
    Write-Host "Serveur ouvert sur le réseau (port 38281) : ton ami peut se connecter avec ton adresse IP."
}
if ($Password -ne "") {
    $serverArgs += @("--password", $Password)
    Write-Host "Mot de passe joueurs activé."
} else {
    Write-Host "ATTENTION : pas de mot de passe (-Password). N'importe qui joignant le port peut se connecter." -ForegroundColor Yellow
}
Set-Location (Join-Path $root "upstream\Archipelago")
& $python @serverArgs $seedPath
