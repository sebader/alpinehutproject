# How to run offline

1) Install `swa` CLI
1) Install `dab` CLI
1) Install func core tools

Run each in their own terminals:

## Run DAB

Set the environment variable `DATABASE_CONNECTION_STRING` on MacOS by running:

```bash
export DATABASE_CONNECTION_STRING="Server=foobar;"
```



From the `saw-db-connections` folder, run

    dab start -c ./staticwebapp.database.config.json --no-https-redirect true

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

    func start

## Run SAW

From the `website` folder run

    swa start --api-devserver-url http://localhost:7071

Open http://localhost:4280