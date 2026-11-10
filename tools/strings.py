# Source of truth for UI strings. Run: python3 tools/strings.py  (regenerates both .resx files)
# Each entry: key -> (English, Arabic)
S = {
 "App_Name": ("Downora", "Downora"),
 "App_Tagline": ("Universal media downloader", "أداة تنزيل الوسائط الشاملة"),

 "Nav_Home": ("Home", "الرئيسية"),
 "Nav_Downloader": ("Downloader", "أداة التنزيل"),
 "Nav_Queue": ("Download Queue", "قائمة التنزيل"),
 "Nav_Library": ("Media Library", "مكتبة الوسائط"),
 "Nav_History": ("History", "السجل"),
 "Nav_Settings": ("Settings", "الإعدادات"),
 "Nav_About": ("About", "حول"),

 "Shell_Storage": ("Storage ({0})", "التخزين ({0})"),
 "Shell_StorageUsed": ("{0} free of {1}", "{0} متاحة من {1}"),
 "Shell_StorageUnavailable": ("Disk space unavailable", "تعذّر قراءة مساحة القرص"),
 "Shell_ToggleTheme": ("Switch between dark and light", "التبديل بين الوضع الداكن والفاتح"),

 "Home_GreetingMorning": ("Good morning", "صباح الخير"),
 "Home_GreetingAfternoon": ("Good afternoon", "مساء الخير"),
 "Home_GreetingEvening": ("Good evening", "مساء الخير"),
 "Home_Subtitle": ("Your downloads at a glance.", "نظرة سريعة على تنزيلاتك."),
 "Home_StatTotal": ("Total downloads", "إجمالي التنزيلات"),
 "Home_StatTotalHint": ("+{0} this week", "+{0} هذا الأسبوع"),
 "Home_StatData": ("Data downloaded", "البيانات المنزّلة"),
 "Home_StatDataHint": ("Across completed downloads", "من التنزيلات المكتملة"),
 "Home_StatActive": ("Active now", "نشطة الآن"),
 "Home_StatActiveHint": ("{0} in queue", "{0} في القائمة"),
 "Home_StatSuccess": ("Success rate", "نسبة النجاح"),
 "Home_StatSuccessHint": ("Completed vs. failed", "المكتملة مقابل الفاشلة"),
 "Home_ActiveTitle": ("Active downloads", "التنزيلات النشطة"),
 "Home_ViewQueue": ("View queue", "عرض القائمة"),
 "Home_ActiveEmpty": ("Nothing is downloading right now.", "لا يوجد تنزيل جارٍ الآن."),
 "Home_RecentTitle": ("Recently completed", "اكتملت مؤخرًا"),
 "Home_RecentEmpty": ("Completed downloads will appear here.", "ستظهر التنزيلات المكتملة هنا."),
 "Home_SavedIn": ("Saved in {0}", "محفوظة في {0}"),
 "Home_OpenFolder": ("Open download folder", "فتح مجلد التنزيلات"),

 "Downloader_EmptyTitle": ("No link analyzed yet", "لم يتم تحليل أي رابط بعد"),
 "Downloader_EmptyBody": ("When you analyze a link, its title, thumbnail and available formats appear here. Downloads are saved to {0}.", "عند تحليل رابط، سيظهر عنوانه وصورته المصغّرة والصيغ المتاحة هنا. يتم حفظ التنزيلات في {0}."),
 "Downloader_ChangeFolder": ("Change download folder", "تغيير مجلد التنزيلات"),

 "Status_Waiting": ("Waiting", "في الانتظار"),
 "Status_Extracting": ("Extracting", "جارٍ الاستخراج"),
 "Status_Downloading": ("Downloading", "جارٍ التنزيل"),
 "Status_Processing": ("Processing", "جارٍ المعالجة"),
 "Status_Completed": ("Completed", "مكتمل"),
 "Status_Failed": ("Failed", "فشل"),
 "Status_Cancelled": ("Cancelled", "أُلغي"),
 "Status_Paused": ("Paused", "متوقف مؤقتًا"),

 "Queue_FilterAll": ("All", "الكل"),
 "Queue_FilterActive": ("In progress", "قيد التنفيذ"),
 "Queue_EmptyTitle": ("Your queue is empty", "قائمة التنزيل فارغة"),
 "Queue_EmptyBody": ("Downloads you start or queue show up here with live progress.", "تظهر هنا التنزيلات التي تبدأها أو تضيفها مع تقدّمها المباشر."),
 "Queue_FilterEmpty": ("No downloads with this status.", "لا توجد تنزيلات بهذه الحالة."),
 "Queue_SizeOf": ("{0} of {1}", "{0} من {1}"),

 "Library_Search": ("Search by title or artist", "ابحث بالعنوان أو الفنان"),
 "Library_All": ("All", "الكل"),
 "Library_Video": ("Video", "فيديو"),
 "Library_Audio": ("Audio", "صوت"),
 "Library_SortBy": ("Sort by", "ترتيب حسب"),
 "Library_SortDate": ("Date added", "تاريخ الإضافة"),
 "Library_SortTitle": ("Title", "العنوان"),
 "Library_SortDuration": ("Duration", "المدة"),
 "Library_SortSize": ("Size", "الحجم"),
 "Library_Open": ("Open", "فتح"),
 "Library_ShowInFolder": ("Show in folder", "إظهار في المجلد"),
 "Library_Remove": ("Remove from library", "إزالة من المكتبة"),
 "Library_RemoveTip": ("Removes it from this list. The file stays on your disk.", "تزيله من هذه القائمة. يبقى الملف على قرصك."),
 "Library_Missing": ("File missing", "الملف غير موجود"),
 "Library_EmptyTitle": ("Your library is empty", "مكتبتك فارغة"),
 "Library_EmptyBody": ("Finished downloads are added here automatically.", "تُضاف التنزيلات المكتملة هنا تلقائيًا."),
 "Library_NoResults": ("No media matches your search.", "لا توجد وسائط تطابق بحثك."),

 "History_Search": ("Search history", "البحث في السجل"),
 "History_Clear": ("Clear history", "مسح السجل"),
 "History_ClearConfirmTitle": ("Clear download history?", "مسح سجل التنزيلات؟"),
 "History_ClearConfirmBody": ("This removes {0} finished downloads from the list. Files on disk and your media library are not touched.", "سيؤدي هذا إلى إزالة {0} من التنزيلات المنتهية من القائمة. لن يتم المساس بالملفات على القرص أو بمكتبة الوسائط."),
 "History_EmptyTitle": ("No download history", "لا يوجد سجل تنزيلات"),
 "History_EmptyBody": ("Completed, failed and cancelled downloads are listed here.", "تُعرض هنا التنزيلات المكتملة والفاشلة والملغاة."),
 "History_NoResults": ("No downloads match your search or filter.", "لا توجد تنزيلات تطابق بحثك أو الفلتر."),
 "History_FilterAll": ("All", "الكل"),
 "History_FilterCompleted": ("Completed", "المكتملة"),
 "History_FilterFailed": ("Failed", "الفاشلة"),
 "History_FilterCancelled": ("Cancelled", "الملغاة"),
 "History_Remove": ("Remove from history", "إزالة من السجل"),
 "History_RemoveTip": ("Removes this entry. The file and your library are not touched.", "تزيل هذا الإدخال. لا يتم المساس بالملف أو المكتبة."),
 "History_ShowInFolder": ("Show in folder", "إظهار في المجلد"),

 "Settings_Appearance": ("Appearance", "المظهر"),
 "Settings_Theme": ("App theme", "سمة التطبيق"),
 "Settings_ThemeDesc": ("Choose dark or light, or follow Windows.", "اختر الوضع الداكن أو الفاتح، أو اتّبع إعداد Windows."),
 "Theme_System": ("Use system setting", "استخدام إعداد النظام"),
 "Theme_Dark": ("Dark", "داكن"),
 "Theme_Light": ("Light", "فاتح"),
 "Settings_Language": ("Language", "اللغة"),
 "Settings_LanguageDesc": ("Arabic switches the layout to right-to-left.", "تحوّل العربية الواجهة إلى الاتجاه من اليمين إلى اليسار."),
 "Language_English": ("English", "English"),
 "Language_Arabic": ("العربية", "العربية"),
 "Settings_Downloads": ("Downloads", "التنزيلات"),
 "Settings_Folder": ("Download folder", "مجلد التنزيلات"),
 "Settings_FolderDesc": ("New downloads are saved here.", "يتم حفظ التنزيلات الجديدة هنا."),
 "Settings_Browse": ("Browse…", "استعراض…"),
 "Settings_PickFolderTitle": ("Choose a download folder", "اختر مجلد التنزيلات"),
 "Settings_SaveFailed": ("Couldn't save the setting: {0}", "تعذّر حفظ الإعداد: {0}"),
 "Settings_Data": ("Data", "البيانات"),
 "Settings_DataFolder": ("App data", "بيانات التطبيق"),
 "Settings_DataFolderDesc": ("Database, logs and tools are kept here.", "تُحفظ هنا قاعدة البيانات والسجلات والأدوات."),

 "About_Version": ("Version {0}", "الإصدار {0}"),
 "About_Description": ("A native Windows app for downloading and managing video and audio from YouTube, SoundCloud and other supported sites.", "تطبيق Windows أصلي لتنزيل الفيديو والصوت وإدارتهما من YouTube وSoundCloud والمواقع المدعومة الأخرى."),
 "About_Legal": ("Download only content you own or are authorized to access. Downora doesn't remove DRM or bypass access controls.", "نزّل فقط المحتوى الذي تملكه أو المصرّح لك بالوصول إليه. لا يزيل Downora حماية DRM ولا يتجاوز قيود الوصول."),
 "About_Source": ("Source code", "الشيفرة المصدرية"),
 "About_OpenLogs": ("Open logs folder", "فتح مجلد السجلات"),
 "About_ThirdParty": ("Third-party software", "برمجيات الجهات الخارجية"),

 "Common_Paste": ("Paste", "لصق"),
 "Common_Analyze": ("Analyze", "تحليل"),
 "Common_TryAgain": ("Try again", "إعادة المحاولة"),

 "Home_UrlPlaceholder": ("Paste a YouTube, SoundCloud, Vimeo or direct media link…", "الصق رابطًا من YouTube أو SoundCloud أو Vimeo أو رابط وسائط مباشر…"),
 "Home_Supported": ("Works with YouTube, SoundCloud, Vimeo, Dailymotion, direct links and many more sites.", "يعمل مع YouTube وSoundCloud وVimeo وDailymotion والروابط المباشرة ومواقع أخرى كثيرة."),

 "Url_Empty": ("Paste a link first.", "الصق رابطًا أولًا."),
 "Url_NotAUrl": ("That doesn't look like a web link.", "لا يبدو هذا رابط ويب."),
 "Url_UnsupportedScheme": ("Only http and https links are supported.", "الروابط المدعومة هي http وhttps فقط."),
 "Url_TooLong": ("That link is too long.", "هذا الرابط طويل جدًا."),

 "Platform_Other": ("Website", "موقع ويب"),
 "Platform_DirectFile": ("Direct link", "رابط مباشر"),
 "Platform_Unknown": ("Website", "موقع ويب"),

 "Downloader_Analyzing": ("Reading the link…", "جارٍ قراءة الرابط…"),
 "Downloader_AnalyzingHint": ("This usually takes a few seconds.", "يستغرق هذا عادةً بضع ثوانٍ."),
 "Downloader_Qualities": ("Available qualities", "الجودات المتاحة"),
 "Downloader_NotOffered": ("Not offered", "غير متاحة"),
 "Downloader_SizeApprox": ("≈ {0}", "≈ {0}"),
 "Downloader_SizeUnknown": ("Size unknown", "الحجم غير معروف"),
 "Downloader_BestAudio": ("Best audio: {0}", "أفضل صوت: {0}"),
 "Downloader_Live": ("This is a live stream. Downloading live streams isn't supported.", "هذا بث مباشر. تنزيل البث المباشر غير مدعوم."),
 "Downloader_OpenInBrowser": ("Open in browser", "فتح في المتصفح"),
 "Downloader_SavedTo": ("Downloads are saved to {0}", "يتم حفظ التنزيلات في {0}"),
 "Downloader_Playlist": ("Playlist", "قائمة تشغيل"),
 "Downloader_AsVideo": ("Video", "فيديو"),
 "Downloader_AsAudio": ("Audio", "صوت"),
 "Downloader_PlaylistFormat": ("Download as", "تنزيل بصيغة"),
 "Downloader_SelectAll": ("Select all", "تحديد الكل"),
 "Downloader_SelectNone": ("Select none", "إلغاء التحديد"),
 "Downloader_SelectedCount": ("{0} selected", "تم تحديد {0}"),
 "Downloader_DownloadSelected": ("Download selected", "تنزيل المحدد"),
 "Downloader_PlaylistAdded": ("{0} items added to your queue.", "تمت إضافة {0} عنصرًا إلى قائمة التنزيل."),
 "Downloader_PlaylistSkipped": ("{0} already in your queue were skipped.", "تم تخطي {0} موجودة بالفعل في القائمة."),
 "Downloader_PlaylistNothingNew": ("Everything you picked is already in your queue.", "كل ما اخترته موجود بالفعل في قائمة التنزيل."),
 "Downloader_PlaylistProgress": ("{0} of {1} finished", "اكتمل {0} من {1}"),
 "Downloader_PlaylistFailed": ("{0} failed", "فشل {0}"),
 "Downloader_PlaylistCount": ("{0} items", "{0} عنصرًا"),
 "Downloader_PlaylistTotal": ("Total {0}", "المدة الكلية {0}"),
 "Downloader_EntryUnavailable": ("Unavailable", "غير متاح"),
 "Downloader_Details": ("Details from yt-dlp: {0}", "تفاصيل من yt-dlp: {0}"),
 "Downloader_InstallTools": ("Install required tools", "تثبيت الأدوات المطلوبة"),
 "Downloader_Installing": ("Downloading {0}… {1}", "جارٍ تنزيل {0}… {1}"),

 "Quality_Best": ("Best available", "أفضل جودة متاحة"),
 "Quality_P2160": ("4K (2160p)", "4K ‏(2160p)"),
 "Quality_P1440": ("1440p", "1440p"),
 "Quality_P1080": ("1080p", "1080p"),
 "Quality_P720": ("720p", "720p"),
 "Quality_P480": ("480p", "480p"),
 "Quality_P360": ("360p", "360p"),
 "Quality_AudioOnly": ("Audio only", "صوت فقط"),

 "Error_Unsupported_Title": ("This link isn't supported", "هذا الرابط غير مدعوم"),
 "Error_Unsupported_Body": ("Downora couldn't find video or audio on this page. Check the link, or open the page of the video itself.", "لم يعثر Downora على فيديو أو صوت في هذه الصفحة. تحقّق من الرابط أو افتح صفحة الفيديو نفسها."),
 "Error_RequiresSignIn_Title": ("Sign-in required", "يلزم تسجيل الدخول"),
 "Error_RequiresSignIn_Body": ("The site only shows this to signed-in users, or asked to confirm you're not a bot.", "لا يعرض الموقع هذا المحتوى إلا للمستخدمين المسجّلين، أو طلب التأكد من أنك لست برنامجًا آليًا."),
 "Error_Drm_Title": ("Protected content", "محتوى محمي"),
 "Error_Drm_Body": ("This content is protected with DRM, which Downora doesn't remove.", "هذا المحتوى محمي بتقنية DRM ولا يزيلها Downora."),
 "Error_GeoRestricted_Title": ("Not available in your region", "غير متاح في منطقتك"),
 "Error_GeoRestricted_Body": ("The site doesn't offer this content in your country.", "لا يتيح الموقع هذا المحتوى في بلدك."),
 "Error_Unavailable_Title": ("Content unavailable", "المحتوى غير متاح"),
 "Error_Unavailable_Body": ("It may have been removed or made private, or the link may be wrong.", "ربما تمت إزالته أو جعله خاصًا، أو أن الرابط غير صحيح."),
 "Error_Network_Title": ("Couldn't reach the site", "تعذّر الوصول إلى الموقع"),
 "Error_Network_Body": ("Check your internet connection and try again. If it keeps happening, the site may be limiting requests for a while.", "تحقّق من اتصالك بالإنترنت وأعد المحاولة. إذا تكرر ذلك فقد يكون الموقع يحدّ من الطلبات مؤقتًا."),
 "Error_ToolMissing_Title": ("Required tools aren't installed", "الأدوات المطلوبة غير مثبّتة"),
 "Error_ToolMissing_Body": ("Downora uses yt-dlp to read and download links, FFmpeg to join and convert media, and Deno for YouTube. They're downloaded from their official releases and checked before use.", "يستخدم Downora أداة yt-dlp لقراءة الروابط وتنزيلها، وFFmpeg لدمج الوسائط وتحويلها، وDeno لروابط YouTube. يتم تنزيلها من الإصدارات الرسمية والتحقق منها قبل الاستخدام."),
 "Error_DiskFull_Title": ("The disk is full", "القرص ممتلئ"),
 "Error_DiskFull_Body": ("Free some space or choose another download folder in Settings.", "أفرغ بعض المساحة أو اختر مجلد تنزيل آخر من الإعدادات."),
 "Error_Unknown_Title": ("Something went wrong", "حدث خطأ ما"),
 "Error_Unknown_Body": ("Downora couldn't finish this. The details below may help.", "تعذّر على Downora إكمال هذا الإجراء. قد تفيدك التفاصيل أدناه."),
 "Error_InstallFailed": ("Couldn't install: {0}", "تعذّر التثبيت: {0}"),

 "Settings_Windows": ("Windows", "ويندوز"),
 "Settings_LaunchAtStartup": ("Start Downora when I sign in", "تشغيل Downora عند تسجيل الدخول"),
 "Settings_LaunchAtStartupDesc": ("Adds Downora to the apps Windows starts when you sign in. Turn it off here to remove it.", "يضيف Downora إلى التطبيقات التي يشغّلها ويندوز عند تسجيل الدخول. أوقفه هنا لإزالته."),
 "Settings_StartMinimized": ("Start in the tray", "البدء في شريط المهام"),
 "Settings_StartMinimizedDesc": ("When Downora starts by itself at sign-in, it stays in the tray instead of opening its window.", "عندما يبدأ Downora تلقائيًا عند تسجيل الدخول، يبقى في شريط المهام بدل فتح نافذته."),
 "Settings_CloseToTray": ("Keep running in the tray when I close the window", "الاستمرار في العمل بشريط المهام عند إغلاق النافذة"),
 "Settings_CloseToTrayDesc": ("Downloads continue in the background. Use Exit in the tray menu to quit Downora.", "تستمر التنزيلات في الخلفية. استخدم «خروج» من قائمة الشريط لإغلاق Downora."),
 "Settings_KeepHistory": ("Keep download history", "الاحتفاظ بسجل التنزيلات"),
 "Settings_KeepHistoryDesc": ("Older entries are removed from History. Your files and media library are not touched.", "تُزال الإدخالات الأقدم من السجل. لا يتم المساس بملفاتك أو بمكتبة الوسائط."),
 "Settings_KeepForever": ("Forever", "للأبد"),
 "Settings_Keep30": ("30 days", "30 يومًا"),
 "Settings_Keep90": ("90 days", "90 يومًا"),
 "Settings_Keep180": ("180 days", "180 يومًا"),
 "Settings_Keep365": ("1 year", "سنة واحدة"),
 "Settings_YtDlpAutoUpdate": ("Keep yt-dlp up to date", "إبقاء yt-dlp محدّثًا"),
 "Settings_YtDlpAutoUpdateDesc": ("Checks for a new version when Downora starts, at most once a day and never while downloads are running. Sites change often, and an old yt-dlp stops working.", "يتحقق من وجود إصدار جديد عند بدء Downora، مرة واحدة يوميًا على الأكثر وليس أثناء التنزيل. تتغير المواقع كثيرًا، وإصدار yt-dlp القديم يتوقف عن العمل."),
 "Settings_FfmpegOwn": ("Use my own FFmpeg", "استخدام FFmpeg الخاص بي"),
 "Settings_FfmpegOwnDesc": ("Pick an ffmpeg.exe you installed yourself instead of the one Downora manages.", "اختر ملف ffmpeg.exe ثبّتَّه بنفسك بدلًا من النسخة التي يديرها Downora."),
 "Settings_FfmpegBundled": ("Using the copy Downora manages", "يتم استخدام النسخة التي يديرها Downora"),
 "Settings_FfmpegPickTitle": ("Choose ffmpeg.exe", "اختر ffmpeg.exe"),
 "Settings_FfmpegUseBundled": ("Use Downora's copy", "استخدام نسخة Downora"),
 "Tray_Open": ("Open Downora", "فتح Downora"),
 "Tray_PauseAll": ("Pause all downloads", "إيقاف كل التنزيلات مؤقتًا"),
 "Tray_ResumeAll": ("Resume all downloads", "استئناف كل التنزيلات"),
 "Tray_Exit": ("Exit", "خروج"),
 "Tray_Active": ("Downora: {0} downloading", "Downora: {0} قيد التنزيل"),
 "Settings_Tools": ("Tools", "الأدوات"),
 "Tool_YtDlp": ("yt-dlp", "yt-dlp"),
 "Tool_YtDlpDesc": ("Reads links and downloads media.", "يقرأ الروابط وينزّل الوسائط."),
 "Tool_Ffmpeg": ("FFmpeg and FFprobe", "FFmpeg وFFprobe"),
 "Tool_FfmpegDesc": ("Merges, converts and checks media files.", "يدمج ملفات الوسائط ويحوّلها ويتحقق منها."),
 "Tool_Deno": ("Deno", "Deno"),
 "Tool_DenoDesc": ("JavaScript runtime yt-dlp needs for YouTube.", "بيئة تشغيل JavaScript يحتاجها yt-dlp لروابط YouTube."),
 "Tool_NotInstalled": ("Not installed", "غير مثبّت"),
 "Tool_Version": ("Version {0}", "الإصدار {0}"),
 "Tool_InstalledNoVersion": ("Installed", "مثبّت"),
 "Tool_Install": ("Install", "تثبيت"),
 "Tool_Update": ("Check for updates", "التحقق من التحديثات"),
 "Tool_Checking": ("Checking…", "جارٍ التحقق…"),
 "Common_Open": ("Open", "فتح"),
 "Common_Cancel": ("Cancel", "إلغاء"),
 "Common_Refresh": ("Refresh", "تحديث"),
 "Error_BrowserCookies_Title": ("Couldn't read your browser sign-in", "تعذّرت قراءة تسجيل الدخول من المتصفح"),
 "Error_CorruptFile_Title": ("The downloaded file is damaged", "الملف الذي تم تنزيله تالف"),
 "Error_CorruptFile_Body": ("Downora checked the file after downloading and it did not pass, so it was removed. Try again; if it keeps happening the source may be serving a broken stream.", "فحص Downora الملف بعد التنزيل ولم يجتز الفحص، لذلك تم حذفه. أعد المحاولة، وإن تكرر الأمر فقد يكون المصدر يقدّم بثًّا تالفًا."),
 "Settings_EmbedMetadata": ("Add title and artist to files", "إضافة العنوان والفنان إلى الملفات"),
 "Settings_EmbedMetadataDesc": ("Writes the title, uploader and date into the file's own tags, so music players and file properties show them.", "تكتب العنوان والناشر والتاريخ في وسوم الملف نفسه ليعرضها مشغّل الموسيقى وخصائص الملف."),
 "Settings_EmbedCover": ("Add cover art to files", "إضافة صورة الغلاف إلى الملفات"),
 "Settings_EmbedCoverDesc": ("Puts the thumbnail inside the file as its cover picture. Not possible for WAV and WebM.", "تضع الصورة المصغّرة داخل الملف كصورة غلاف. غير ممكن لصيغتي WAV وWebM."),
 "Error_BrowserCookies_Body": ("Close the browser and try again, or choose another browser in Settings. Some browsers lock their sign-in data while they are open.", "أغلق المتصفح وأعد المحاولة، أو اختر متصفحًا آخر من الإعدادات. تقفل بعض المتصفحات بيانات تسجيل الدخول أثناء تشغيلها."),

 "Downloader_Download": ("Download", "تنزيل"),
 "Downloader_DownloadAudio": ("Download audio", "تنزيل الصوت"),
 "Downloader_PickQuality": ("Pick a quality", "اختر الجودة"),
 "Downloader_AudioTitle": ("Audio only", "صوت فقط"),
 "Downloader_AudioFormat": ("Audio format", "صيغة الصوت"),
 "Downloader_Started": ("Added to your queue.", "تمت الإضافة إلى قائمة التنزيل."),
 "Downloader_AlreadyActive": ("This is already in your queue at that quality.", "هذا العنصر موجود بالفعل في قائمة التنزيل بالجودة نفسها."),
 "Downloader_UseBrowserSignIn": ("Use my browser sign-in", "استخدام تسجيل الدخول من متصفحي"),
 "Downloader_OpenSignInSettings": ("Open sign-in settings", "فتح إعدادات تسجيل الدخول"),
 "Downloader_SignInHint": ("Downora will only use your browser's sign-in if you turn it on in Settings.", "لن يستخدم Downora تسجيل الدخول من متصفحك إلا إذا فعّلته من الإعدادات."),

 "AudioFormat_Mp3": ("MP3", "MP3"),
 "AudioFormat_M4a": ("M4A (AAC)", "M4A (AAC)"),
 "AudioFormat_Opus": ("Opus", "Opus"),
 "AudioFormat_Flac": ("FLAC (lossless)", "FLAC (بدون فقد)"),
 "AudioFormat_Wav": ("WAV (uncompressed)", "WAV (غير مضغوط)"),

 "Queue_Eta": ("{0} left", "متبقي {0}"),
 "Queue_OpenFolder": ("Open folder", "فتح المجلد"),
 "Queue_ShowFile": ("Show file", "إظهار الملف"),
 "Stage_Starting": ("Starting…", "جارٍ البدء…"),
 "Stage_Merging": ("Joining video and audio…", "جارٍ دمج الفيديو والصوت…"),
 "Stage_PostProcessing": ("Finishing up…", "جارٍ إنهاء العملية…"),
 "Stage_Verifying": ("Checking the file…", "جارٍ فحص الملف…"),
 "Stage_Cancelling": ("Cancelling…", "جارٍ الإلغاء…"),

 "Settings_Cookies": ("Browser sign-in", "تسجيل الدخول من المتصفح"),
 "Settings_CookiesUse": ("Use my browser sign-in", "استخدام تسجيل الدخول من متصفحي"),
 "Settings_CookiesDesc": ("Lets Downora use the sign-in your browser already has, so you can download content your own account is allowed to see, such as members-only or age-restricted videos. It is off unless you turn it on. Downora never copies, stores or logs your cookies: yt-dlp reads them from the browser each time it runs. Browsers such as Chrome and Edge may need to be closed first.", "يتيح لـ Downora استخدام تسجيل الدخول الموجود في متصفحك لتنزيل المحتوى المسموح لحسابك برؤيته، مثل الفيديوهات الخاصة بالأعضاء أو المقيّدة بالعمر. الخيار متوقف ما لم تفعّله. لا ينسخ Downora ملفات تعريف الارتباط ولا يخزّنها ولا يسجّلها: تقرؤها أداة yt-dlp من المتصفح في كل مرة تعمل فيها. قد يلزم إغلاق متصفحات مثل Chrome وEdge أولًا."),
 "Settings_CookiesBrowser": ("Browser", "المتصفح"),
 "Settings_CookiesBrowserDesc": ("Which browser's sign-in to use.", "أي متصفح يُستخدم تسجيل الدخول منه."),
 "Browser_Edge": ("Microsoft Edge", "Microsoft Edge"),
 "Browser_Chrome": ("Google Chrome", "Google Chrome"),
 "Browser_Firefox": ("Mozilla Firefox", "Mozilla Firefox"),
 "Browser_Brave": ("Brave", "Brave"),

 "Queue_Pause": ("Pause", "إيقاف مؤقت"),
 "Queue_Resume": ("Resume", "استئناف"),
 "Queue_Retry": ("Try again", "إعادة المحاولة"),
 "Queue_PauseAll": ("Pause all", "إيقاف الكل مؤقتًا"),
 "Queue_ResumeAll": ("Resume all", "استئناف الكل"),
 "Queue_SummaryRunning": ("{0} downloading", "{0} قيد التنزيل"),
 "Queue_SummaryWaiting": ("{0} waiting", "{0} في الانتظار"),
 "Queue_SummaryPaused": ("{0} paused", "{0} متوقفة مؤقتًا"),
 "Stage_Queued": ("Waiting for a free slot…", "في انتظار مكان شاغر…"),
 "Stage_Paused": ("Paused. What is downloaded so far is kept.", "متوقف مؤقتًا. يتم الاحتفاظ بما تم تنزيله."),

 "Settings_Concurrent": ("Simultaneous downloads", "التنزيلات المتزامنة"),
 "Settings_ConcurrentDesc": ("How many downloads run at once. The rest wait in the queue.", "عدد التنزيلات التي تعمل في وقت واحد. تنتظر البقية في القائمة."),
 "Settings_AutoStart": ("Start downloads automatically", "بدء التنزيلات تلقائيًا"),
 "Settings_AutoStartDesc": ("When off, new downloads are held in the queue until you resume them.", "عند الإيقاف، تبقى التنزيلات الجديدة موقوفة في القائمة حتى تستأنفها."),

 "Error_Generic": ("Something went wrong: {0}", "حدث خطأ ما: {0}"),
}

import html, os
HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
'''
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "src", "Downora.Presentation", "Localization")
for idx, suffix in ((0, ""), (1, ".ar")):
    lines = [HEADER]
    for k, v in S.items():
        lines.append(f'  <data name="{k}" xml:space="preserve"><value>{html.escape(v[idx], quote=False)}</value></data>\n')
    lines.append("</root>\n")
    with open(os.path.join(out, f"Strings{suffix}.resx"), "w", encoding="utf-8", newline="\n") as f:
        f.write("".join(lines))
print(len(S), "strings")
