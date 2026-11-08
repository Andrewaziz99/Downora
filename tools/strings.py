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
 "History_NoResults": ("No downloads match your search.", "لا توجد تنزيلات تطابق بحثك."),
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

 "Common_Open": ("Open", "فتح"),
 "Common_Cancel": ("Cancel", "إلغاء"),
 "Common_Refresh": ("Refresh", "تحديث"),
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
