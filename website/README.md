# How to run offline

1) Install `swa` CLI
1) Install func core tools

Run each in their own terminals:

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

## Run SWA

From the `website` folder run

    swa start --api-devserver-url http://localhost:7071

Open http://localhost:4280