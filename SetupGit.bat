@echo off
git config --global lfs.customtransfer.gdrivelfs_extra.path ".lfstool/GoogleDriveLFS.exe"
git config --global lfs."https://drive-extra.google.com".standalonetransferagent gdrivelfs_extra

IF  '%ERRORLEVEL%'=='0' (
  echo GoogleDriveLFS setup successful
) ELSE (
  echo Error occured while setting up GoogleDriveLFS: %ERRORLEVEL%
)

pause