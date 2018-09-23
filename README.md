# AtlantTest
## Bitcoin Api

### Setup
* Create empty database
* Configure connection string in Web.config to this database
* Open CreateDatabase.sql and scroll to the bottom of the script (physical path: .sql\CreateDatabase.sql)
* Fill Wallet's details according to comments and run script on created database
* Build and run applictaion using IIS

### API

* GET:  /api/getLast
* POST: /api/sendbtc
** POST BODY: 
```json
{
  "address": "mv4rnyY3Su5gjcDNzbMLKBQkBicCtHUtFB",
  "amount": "0.00001"
}
```

