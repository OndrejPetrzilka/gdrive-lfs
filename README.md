Custom transfer agent for Git-lfs which uses Google Drive as storage

When you build it, installer NSIS installer will be created
- installs GoogleDriveLFS.exe into program files
- sets up git config in following manner

```
[lfs "customtransfer.gdrivelfs"]
	path = C:\\Program Files (x86)\\GoogleDriveLFS\\GoogleDriveLFS.exe
[lfs "https://drive.google.com"]
	standalonetransferagent = gdrivelfs
```

## Repository setup
1. Create `.lfsconfig` in the root with
   ```
   [lfs]
   url = https://drive.google.com
   ```
2. Create service account in Google console

   https://help.talend.com/en-US/components/8.0/google-drive/how-to-access-google-drive-using-service-account-json-file-a-google
3. Download service account settings as JSON
4. Save it as `.gdrivelfs` in the root of repository
5. Open it and add `"drive_id": XXX` to the end
   
   Drive id can be found in URL when you open google drive

## Example .gdrivelfs 
```
{
  "type": "service_account",
  "project_id": "XXXXXXXXXXXX",
  "private_key_id": "XXXXXXXXXXXX",
  "private_key": "XXXXXXXXXXXXXXXXX\n",
  "client_email": "googledrivelfs@XXXXXXXXXXXX.iam.gserviceaccount.com",
  "client_id": "XXXXXXXXXXXX",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token",
  "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
  "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/googledrivelfs%40XXXXXXXXXXXX.iam.gserviceaccount.com",
  "universe_domain": "googleapis.com",
  "drive_id": "000005QfFhUSAAAAAAA"
}
```
