# How to run offline

1) Install `swa` CLI
1) Install `dab` CLI
1) Install func core tools

Run each in their own terminals:

## Run DAB

Set ENV variable for `DATABASE_CONNECTION_STRING`.

From the `saw-db-connections` folder, run

    dab start -c .\staticwebapp.database.config.json --no-https-redirect true

## Run Backend Functions

Create local.settings.json like

```
{
    "IsEncrypted": false,
    "values": {
        "DatabaseConnectionString": "Server=foobar;"
    }
}
```

From `WebsiteBackendFunctions` folder run

    func start --csharp --port 7000

## Run SAW

From the `website` folder run

    swa start --api-devserver-url http://localhost:7000 --data-api-devserver-url http://localhost:5000


Open http://localhost:4280