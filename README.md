# Task Timer

**English** | [العربية](#العربية)

Task Timer is a Windows desktop app that loads your work items from Azure DevOps (or issues from GitHub), times the work you do on them, and sends the completed time back to the server. It also handles Microsoft Teams calls and meetings, pauses when you are away, and tracks your working time on the computer.

## Install

### Requirements

- Windows 10 / 11 or Windows Server, 64-bit.
- A personal access token (PAT):
  - **Azure DevOps**: scope *Work Items (Read & Write)*. Add *Project and Team (Read)* if you leave the project empty and want the full sprint list.
  - **GitHub**: `repo` scope, or Issues read/write on a fine-grained token.
- The .NET 10 SDK is needed only to build from source. A PC that receives the ready-made `publish` folder needs nothing extra.

### Steps

1. Run `install.cmd` (double-click it). No administrator rights are needed.
2. The installer builds the app, copies it to `%LOCALAPPDATA%\Programs\TaskTimer`, adds a Start menu shortcut and a Startup shortcut, and starts the app.
3. Task Timer now starts automatically every time you sign in to Windows.

To install on another PC, copy `install.cmd` together with the `publish` folder and run it there.

**Upgrade**: run `install.cmd` again. Stop your running task first, because the installer closes the app.

**Uninstall**: run `uninstall.cmd`. Your settings and time log in `%APPDATA%\TaskTimer` are kept.

**MSI installer**: run `dotnet build TaskTimer.Installer -c Release`. It creates `TaskTimer.Installer\bin\Release\TaskTimerSetup.msi`, which installs Task Timer for all users (administrator rights needed), adds a Start menu shortcut and starts the app at sign-in.

**Build only**: run `build.cmd`. It creates the `publish` folder without installing anything.

**Run from source without installing**:

```
dotnet run --project TaskTimer
```

## First-time setup

The **Connection settings** form opens on first start. You can reopen it any time from the toolbar.

| Field | Meaning |
|---|---|
| Provider | Azure DevOps or GitHub. |
| Organization URL | For example `https://dev.azure.com/your-organization`. For GitHub, leave empty unless you use GitHub Enterprise. |
| Project (optional) | Limits the list to one Azure DevOps project. Required when you use the Team field with a plain team name. |
| Team (optional) | Team name. When set, tasks assigned to any member of the team are loaded too. You can also write `[Project]\Team`. |
| Customer field | The Azure DevOps field that holds the customer, by reference name. Default is the area path (`System.AreaPath`); use for example `Custom.Customer` for a custom field. |
| Pause timer after no activity | Seconds without keyboard or mouse input before the task timer pauses. `0` turns it off. |
| Work folders | Folders with your work files, separated by `;`. Opening or saving a file there starts the timer. |
| Personal access token | Stored encrypted for your Windows user. |
| Detect Microsoft Teams calls | Pauses the task and times the call automatically. |
| Continue the last running task | Resumes the task that was running when the app was closed. |
| Reduce Remaining Work / Add a comment | What else to update in Azure DevOps when time is sent. |

Press **Test connection**, then **Save**.

## Daily use

### The task list

- **Sync** reloads the list. It also refreshes by itself every 15 minutes.
- Tasks are shown under their parents (Epic → Feature → Story → Task). Grey italic rows are parents shown for context only; they cannot be timed.
- Filters on the toolbar: **Sprint**, **Status**, **Customer**, **Assigned to**. Your choices are remembered.
- With "All sprints" you see open tasks only. Selecting a sprint loads every task in it, including closed ones, so ended sprints are not empty.

### Timing a task

1. Select a task and press **Start task** (or double-click the task).
2. Press **Stop task** when you finish. Starting another task stops the current one.
3. The **Not yet sent** column shows time recorded locally and not yet sent to the server.

### Sending time to the server

Select the task and press **Send time to server**, then confirm the hours.

- **Azure DevOps**: the hours are added to *Completed Work*. Optionally *Remaining Work* is reduced and a comment is added.
- **GitHub**: issues have no time field, so the hours are posted as a comment on the issue.

### Calls and meetings

- When a Teams call or meeting starts, the task timer pauses and the call timer starts. A prompt asks whether it is a call or a meeting and where the time should go: the current task, a different task, or "ask me when it ends".
- When it ends, a second prompt shows the duration and asks again. You can also choose not to record it. The task timer then resumes.
- Use **Start call / meeting** to do the same manually, for example for calls outside Teams.

### What the app does by itself

- **Idle pause**: pauses the task timer after the configured seconds without input, and resumes on the next key press or mouse move.
- **Lock and Remote Desktop**: pauses immediately when the computer is locked or the remote connection drops, and resumes when you are back.
- **Work files**: starts timing your current (or last) task when you open or save a file in a work folder.
- **Working time today**: the status bar shows the time the computer was actively used today, the time recorded on tasks, and the time spent in a Remote Desktop session.

### Closing the window

Closing the window does not stop the app. It keeps running in the background with an icon in the notification area (the hidden icons next to the clock). Double-click the icon to open the window again; right-click it and choose **Exit** to close the app completely. Starting Task Timer again while it is running just brings its window back.

### Language

The button at the end of the toolbar switches between English and Arabic. The layout flips to right-to-left in Arabic.

## Good to know

- Teams detection checks whether Teams is using the microphone. It cannot tell a call from a meeting, so the app asks you.
- File opens are detected through the Windows Recent list and through saves in the work folders. A file opened inside an editor and only read may not be noticed.
- Time is saved to disk when a timer stops. If the app crashes, the stretch that was running is lost.
- Some Azure DevOps work item types have no *Completed Work* field. Sending time to them shows a server error and the time stays unsent.
- Your data is in `%APPDATA%\TaskTimer`: `settings.json`, `timelog.json` and `activity.json`.

---

<div dir="rtl">

## العربية

[English](#task-timer) | **العربية**

«مؤقت المهام» تطبيق لسطح مكتب ويندوز يحمّل عناصر العمل المسندة إليك من Azure DevOps (أو المشكلات من GitHub)، ويحسب الوقت الذي تقضيه في العمل عليها، ثم يرسل الوقت المنجز إلى الخادم. يتعامل التطبيق أيضاً مع مكالمات واجتماعات Microsoft Teams، ويتوقف مؤقتاً عند غيابك، ويتتبع وقت عملك على الحاسوب.

## التثبيت

### المتطلبات

- ويندوز 10 أو 11 أو Windows Server بنظام 64 بت.
- رمز وصول شخصي (PAT):
  - **Azure DevOps**: صلاحية *Work Items (Read & Write)*. أضف *Project and Team (Read)* إذا تركت حقل المشروع فارغاً وأردت قائمة السبرنتات كاملة.
  - **GitHub**: صلاحية `repo`، أو قراءة وكتابة المشكلات (Issues) في الرموز الدقيقة الصلاحيات.
- حزمة ‎.NET 10 SDK مطلوبة فقط للبناء من الشيفرة المصدرية. الحاسوب الذي يستلم مجلد `publish` الجاهز لا يحتاج شيئاً إضافياً.

### الخطوات

1. شغّل الملف `install.cmd` (بالنقر المزدوج). لا تحتاج إلى صلاحيات المسؤول.
2. يبني المثبّت التطبيق وينسخه إلى `%LOCALAPPDATA%\Programs\TaskTimer`، ويضيف اختصاراً في قائمة ابدأ واختصاراً في مجلد بدء التشغيل، ثم يشغّل التطبيق.
3. بعد ذلك يعمل «مؤقت المهام» تلقائياً في كل مرة تسجّل فيها الدخول إلى ويندوز.

للتثبيت على حاسوب آخر، انسخ الملف `install.cmd` مع مجلد `publish` وشغّله هناك.

**الترقية**: شغّل `install.cmd` مرة أخرى. أوقف المهمة الجارية أولاً لأن المثبّت يغلق التطبيق.

**إزالة التثبيت**: شغّل `uninstall.cmd`. تبقى إعداداتك وسجل الوقت محفوظة في `%APPDATA%\TaskTimer`.

**مثبّت MSI**: شغّل `dotnet build TaskTimer.Installer -c Release`. ينشئ الملف `TaskTimer.Installer\bin\Release\TaskTimerSetup.msi` الذي يثبّت «مؤقت المهام» لجميع المستخدمين (يحتاج صلاحيات المسؤول)، ويضيف اختصاراً في قائمة ابدأ، ويشغّل التطبيق عند تسجيل الدخول.

**البناء فقط**: شغّل `build.cmd`. ينشئ مجلد `publish` دون تثبيت أي شيء.

**التشغيل من الشيفرة المصدرية دون تثبيت**:

</div>

```
dotnet run --project TaskTimer
```

<div dir="rtl">

## الإعداد لأول مرة

تظهر نافذة **إعدادات الاتصال** عند أول تشغيل، ويمكنك فتحها في أي وقت من شريط الأدوات.

| الحقل | المعنى |
|---|---|
| المزوّد | Azure DevOps أو GitHub. |
| رابط المؤسسة | مثال: `https://dev.azure.com/your-organization`. في GitHub اتركه فارغاً إلا إذا كنت تستخدم GitHub Enterprise. |
| المشروع (اختياري) | يحصر القائمة في مشروع واحد من Azure DevOps. مطلوب إذا كتبت اسم الفريق فقط في حقل الفريق. |
| الفريق (اختياري) | اسم الفريق. عند تعبئته تُحمَّل أيضاً المهام المسندة إلى أي عضو في الفريق. يمكنك كتابته بالصيغة `[Project]\Team`. |
| حقل العميل | الحقل الذي يحمل اسم العميل في Azure DevOps، بالاسم المرجعي. الافتراضي هو مسار المنطقة (`System.AreaPath`)، واستخدم مثلاً `Custom.Customer` للحقل المخصص. |
| إيقاف المؤقت بعد عدم النشاط | عدد الثواني دون لوحة مفاتيح أو فأرة قبل أن يتوقف مؤقت المهمة مؤقتاً. القيمة `0` تعطّل الخاصية. |
| مجلدات العمل | المجلدات التي تحوي ملفات عملك، مفصولة بـ `;`. فتح ملف فيها أو حفظه يبدأ المؤقت. |
| رمز الوصول الشخصي | يُحفظ مشفّراً لمستخدم ويندوز الحالي. |
| اكتشاف مكالمات Microsoft Teams | يوقف المهمة مؤقتاً ويحسب وقت المكالمة تلقائياً. |
| متابعة آخر مهمة كانت تعمل | يستأنف المهمة التي كانت تعمل عند إغلاق التطبيق. |
| إنقاص العمل المتبقي / إضافة تعليق | ما يُحدَّث أيضاً في Azure DevOps عند إرسال الوقت. |

اضغط **اختبار الاتصال** ثم **حفظ**.

## الاستخدام اليومي

### قائمة المهام

- زر **مزامنة** يعيد تحميل القائمة، وتتحدّث القائمة تلقائياً كل 15 دقيقة.
- تظهر المهام تحت العناصر الأب (Epic ← Feature ← Story ← Task). الصفوف الرمادية المائلة عناصر أب للتوضيح فقط ولا يمكن حساب الوقت عليها.
- عوامل التصفية في شريط الأدوات: **السبرنت**، **الحالة**، **العميل**، **مسند إلى**. يتذكر التطبيق اختياراتك.
- عند اختيار «جميع السبرنتات» تظهر المهام المفتوحة فقط. اختيار سبرنت محدد يحمّل كل مهامه بما فيها المغلقة، فلا تظهر السبرنتات المنتهية فارغة.

### حساب وقت مهمة

1. اختر مهمة واضغط **بدء المهمة** (أو انقر عليها نقراً مزدوجاً).
2. اضغط **إيقاف المهمة** عند الانتهاء. بدء مهمة أخرى يوقف المهمة الحالية.
3. يعرض عمود **لم يُرسل بعد** الوقت المسجّل محلياً ولم يُرسل إلى الخادم.

### إرسال الوقت إلى الخادم

اختر المهمة واضغط **إرسال الوقت إلى الخادم** ثم أكّد عدد الساعات.

- **Azure DevOps**: تُضاف الساعات إلى حقل *Completed Work*. ويمكن اختيارياً إنقاص *Remaining Work* وإضافة تعليق.
- **GitHub**: لا يوجد حقل للوقت في المشكلات، لذلك تُكتب الساعات كتعليق على المشكلة.

### المكالمات والاجتماعات

- عند بدء مكالمة أو اجتماع في Teams يتوقف مؤقت المهمة ويبدأ مؤقت المكالمة. تظهر نافذة تسألك هل هي مكالمة أم اجتماع، وأين يُسجَّل الوقت: على المهمة الحالية، أو مهمة أخرى، أو «اسألني عند الانتهاء».
- عند الانتهاء تظهر نافذة ثانية تعرض المدة وتسألك مرة أخرى، ويمكنك اختيار عدم تسجيل الوقت. بعدها يُستأنف مؤقت المهمة.
- استخدم زر **بدء مكالمة / اجتماع** لفعل ذلك يدوياً، مثلاً للمكالمات خارج Teams.

### ما يفعله التطبيق تلقائياً

- **التوقف عند عدم النشاط**: يوقف مؤقت المهمة بعد عدد الثواني المحدد دون إدخال، ويستأنفه عند أول ضغطة مفتاح أو حركة فأرة.
- **القفل وسطح المكتب البعيد**: يتوقف فوراً عند قفل الحاسوب أو انقطاع الاتصال عن بُعد، ويُستأنف عند عودتك.
- **ملفات العمل**: يبدأ حساب الوقت على مهمتك الحالية (أو الأخيرة) عند فتح ملف في مجلد عمل أو حفظه.
- **وقت العمل اليوم**: يعرض شريط الحالة وقت استخدام الحاسوب الفعلي اليوم، والوقت المسجّل على المهام، والوقت في جلسة سطح المكتب البعيد.

### إغلاق النافذة

إغلاق النافذة لا يوقف التطبيق، بل يبقى يعمل في الخلفية مع أيقونة في منطقة الإشعارات (الأيقونات المخفية بجانب الساعة). انقر على الأيقونة نقراً مزدوجاً لفتح النافذة من جديد، أو انقر عليها بالزر الأيمن واختر **خروج** لإغلاق التطبيق نهائياً. تشغيل «مؤقت المهام» مرة أخرى وهو يعمل يعيد إظهار نافذته فقط.

### اللغة

الزر في نهاية شريط الأدوات يبدّل بين العربية والإنجليزية، وينقلب اتجاه الواجهة من اليمين إلى اليسار في العربية.

## معلومات مفيدة

- اكتشاف Teams يعتمد على استخدام Teams للميكروفون، ولا يميّز بين المكالمة والاجتماع، لذلك يسألك التطبيق.
- اكتشاف فتح الملفات يعتمد على قائمة «الملفات الأخيرة» في ويندوز وعلى عمليات الحفظ في مجلدات العمل. قد لا يُلاحَظ ملف فُتح من داخل محرر للقراءة فقط.
- يُحفظ الوقت على القرص عند توقف المؤقت. إذا تعطّل التطبيق يضيع الجزء الذي كان جارياً.
- بعض أنواع عناصر العمل في Azure DevOps لا تحتوي حقل *Completed Work*. إرسال الوقت إليها يُظهر خطأ من الخادم ويبقى الوقت غير مرسل.
- بياناتك موجودة في `%APPDATA%\TaskTimer`: الملفات `settings.json` و`timelog.json` و`activity.json`.

</div>
