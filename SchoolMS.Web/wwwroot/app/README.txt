App update notification
=======================
The app is on Google Play, so phones ask Google Play themselves whether a newer release is live (Play in-app update). When you
publish a new release on Play, old installs show "Update available" the next time the app is opened - nothing to change here.
In Play Console, set the release's "in-app update priority" to 4 or 5 to make an update compulsory.

Optional server-side override (only needed for a copy that is NOT from the Play Store):
create wwwroot/app/version.json  { "latestBuild": 13, "latestVersion": "1.0.5", "apkUrl": "https://play.google.com/store/apps/details?id=com.rkclasses.rk_classes_app", "message": "What is new", "forceUpdate": false }
Phones whose build number (the number after + in the app version) is lower than latestBuild then get the card too.
No version.json = no server-side update message.
