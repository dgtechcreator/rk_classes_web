# RK Classes — Release Notes (11 Nov 2026)

App: **v1.0.6 (build 16)** — pichla release 1.0.5 (build 15) · Web: ek saath publish.
Sab features Web aur Mobile App dono me.

## ✨ New
- **Discontinued Students** — student ke chhodne / nikalne ka record: reason, date, kisne kiya, kitna paid / pending. "Revert" se wapas Active.
- **Batch Holiday** — attendance batch ko holiday mark karo; parents ko "No class today" notification. Cancel karne par "class is on".
- **Birthday wish** — student ke birthday par parent / student login me "Happy Birthday from RK Classes". Text Masters → Templates se edit.
- **Aaj ke Birthdays** — admin dashboard par students + faculty ke birthdays, WhatsApp wish ke saath.
- **Roles & Access** — Student / Parent login me kaun sa section dikhega (Attendance, Lectures, Marks, Fees, Notes, Notifications, Contact) admin on/off kare. Har staff role ke default rights.
- **Payment Modes master** — Cash / Online / Cheque… ab Masters se; Expense, Fee, Teacher Payment sab me wahi list.
- **Teacher Payment — Edit & Delete**, aur Summary ka **Export to Excel**.
- **Faculty Salary Type** — Fixed (monthly) / Hourly / Topic; payment entry me type + rate apne aap.
- **Fee reminder to all due** (web) — ek button se sab due students ke parents ko.
- **Additional charges** (app) — Fee Pay me total charges badlo / hatao.
- **Parent photo** (web) — bachche ki photo web portal se bhi.

## 🔧 Improved
- **Finance** — class + batch + medium wise cards; Total ab discount ke baad (discount sirf info); bade amounts saaf (₹1,23,000 / ₹1.14 L).
- **Expenses** — teacher ki paid salary bhi expense; pichla mahina dekho (‹ ›, Last month, This year, All time, date range); category + search; total poore filter ka.
- **Absent Today** — class → Morning/Evening → medium ke blocks; notification chali gayi to green "Notified".
- **Student delete** — Active → Inactive → Deleted (Deleted sirf Deleted Records me); checkbox se bulk delete; delete ke baad wahi filter.
- **Loaders** — web par upar progress bar + "Please wait", app me har save / delete par "Please wait…".
- **Web ↔ App sync** — Present list grouped, restore wording, sab features barabar.

## 🐞 Fixed
- Marks entry me sab academic years ke students aate the (6 ki jagah 38) — ab sirf chuni hui year. Attendance list me bhi.
- Attendance Batches ke count me inactive students gine ja rahe the.
- Web Expenses → Edit khali form kholta tha aur save par record mit jata tha; Vendor field bhi missing tha.
- Expenses ka total sirf us page ki 15 entries ka tha.
- Section off (jaise Lectures) karne par bhi parent ko uski notifications dikhti thi.

## ⚠️ Admin ke liye
- Pehle **web publish**, phir **naya APK** (purane APK me ye naye features / tabs nahi dikhenge).
- Naya SQL nahi chalana — naye tables / columns site start par apne aap ban jate hain.
- Purane Hourly / Topic teachers ko Faculty me ek baar Salary Type set kar dena (abhi sab "Fixed" maane jate hain).
- Receipts "Cheque" / "Demand Draft" naam se pehchante hain — Payment Modes me in naamon ko rename mat karna.

## 📲 App update popup ke liye (short)
> Naya: Discontinued Students, Batch Holiday, Birthday wish, Roles & Access, Payment Modes, Teacher Payment edit/delete + Excel export, Expenses me salary + pichla mahina, Finance class-batch-medium wise. Fixes: marks entry me sirf selected year ke students.

`version.json` (server par): `{ "latestBuild": 16, "latestVersion": "1.0.6", "apkUrl": "/app/RKClasses.apk", "message": "<upar wala text>", "forceUpdate": false }` — pubspec abhi 1.0.5+15 hai, APK banate waqt 1.0.6+16 karna.
