# ScanTrack Registry

Central registreringsserver för ScanTrack-nätverket.
Driftas av läraren — studenterna anropar den men deployer den inte.

## API

| Endpoint | Metod | Beskrivning |
|----------|-------|-------------|
| `/nodes` | POST | Registrera en nod `{ "city": "Göteborg", "url": "https://..." }` |
| `/nodes` | GET | Hämta alla registrerade noder |
| `/nodes/{city}` | DELETE | Avregistrera en nod |
| `/` | GET | Live-dashboard (auto-refresh var 10s) |

## Deploya lokalt (för test)

```bash
cd ScanTrackRegistry
dotnet run
```

Dashboard: `http://localhost:5000`

## Deploya till Azure App Service

```bash
# Bygg och pusha imagen
docker build -t scantrack-registry ./ScanTrackRegistry
docker tag scantrack-registry <ditt-register>.azurecr.io/scantrack-registry:latest
docker push <ditt-register>.azurecr.io/scantrack-registry:latest

# Skapa App Service (Basic B1 räcker)
az webapp create \
  --resource-group <din-rg> \
  --plan <din-plan> \
  --name scantrack-registry \
  --deployment-container-image-name <ditt-register>.azurecr.io/scantrack-registry:latest
```

URL:en du ger till studenterna: `https://scantrack-registry.azurewebsites.net`

## Notera

Registret lagrar noder in-memory. Om det startas om registrerar sig alla noder om
sig automatiskt vid sin nästa uppstart — inget manuellt arbete krävs.
