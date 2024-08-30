# Org Modifications Since


This utility goes through all of the org's metadata and reports out what metadata has changed since a certain supplied datetime. It also reports out the number of API calls that the utility used.

The number of API calls should be in the ballpark of:
```
Ceilling(Number of Metadata Types / 3) + Ceiling(Number of Report Folders / 3) + Ceiling(Number of Dashboard Folders / 3) + Ceiling(Number of Email Templates Folders / 3)
```

## How to use:

_Authenticate a user_
```
orgmodsin --alias myuser --clientid "client id" --clientsecret "client secret"
```
_List the org's metadata types_
```
orgmodsin --user myuser --list --api 61
```

_Go through all of the Org's metadata_
```
orgmodsin --user myuser --ms "2015-03-23 19:45:00 -7" --api 61
```
_Include only certain metadata types in your search_
```
orgmodsin --user myuser --include "CustomObject,CustomField,ApexClass" --ms "2015-03-23 19:45:00 -7" --api 61
```
_Excludes certain metadata types in your search_
```
orgmodsin --user myuser --exclude "SObject,Report,Dashboard" --ms "2015-03-23 19:45:00 -7" --api 61
```

Note: include takes precedence over exclude, and if both are present exclude will be ignored.

Metadata Types are listed here:
[Salesforce Metadata Types](https://developer.salesforce.com/docs/atlas.en-us.api_meta.meta/api_meta/meta_types_list.htm)
