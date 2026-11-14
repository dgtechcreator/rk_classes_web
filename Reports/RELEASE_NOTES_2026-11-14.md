# RK Classes — Release Notes (14 Nov 2026)

App: **v1.0.5 (build 20)** · Web: published together. All features are available on both Web and the Mobile App unless stated.

## New
- **Teacher Attendance by Stream / Medium** — choose the stream (Science, Commerce, Hindi, English…) along with class and batch.
- **Teacher detail and Excel export** — tap a teacher in the Attendance Summary to see class and medium wise totals and every lecture. Export one teacher or all teachers to Excel.
- **My Details (Student / Parent)** — students and parents can update their date of birth and email.
- **Move to batch** — select several students and change their batch in one step.
- **Faculty vs Users** — the admin home now shows teachers (Faculty) and logins (Users) separately.
- **Live notifications on the web** — new notifications appear instantly instead of after a refresh.

## Improved
- **Teacher attendance time** is shown exactly as entered, for example "4:00 PM – 6:00 PM".
- **Parent full report** shows only Paid and Balance; discount and total fee are hidden.
- **Masters** now show the correct number of active students for classes, sections and batches.
- **Attendance Batches** count each student once, even if the student is in two batches.
- **Faculty list** loads more teachers as you scroll.
- **Tasks tab (app)** keeps completed tasks in the list, faded and listed after open tasks.
- **Phone layout** — Save buttons are no longer hidden behind the navigation bar, and button text no longer breaks on some phones.

## Fixed
- Completing or deleting a task no longer marks all other notifications as read.
- Completed tasks no longer disappear from the app's Tasks tab.
- The progress ring was missing on charts at 100% paid or 100% present.

## For the admin
- Publish the **web first**, then release the **new APK**. Older APKs will not show the new features.
- No SQL needs to be run; new columns are created automatically when the site starts.
- After publishing, please check: counts in Masters, saving date of birth and email from a parent login, teacher attendance with Stream / Medium, and live notifications on the web.

## App update pop-up (short)
> New: Teacher Attendance by Stream / Medium with Excel export, My Details for students and parents, Move to batch, live notifications. Fixes: completed tasks stay in the Tasks tab, correct student counts in Masters.
