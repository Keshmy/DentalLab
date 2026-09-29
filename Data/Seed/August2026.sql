SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

/* ==========================================================
   بيانات شهر 8 / 2026 كما في فواتير المختبر الورقية
   الغرض: التحقق من أن المحرك يعيد إنتاج نفس الإجماليات
   ========================================================== */

-- المراكز والأطباء الناقصون
IF NOT EXISTS (SELECT 1 FROM Clinics WHERE Name = N'مركز بيوتي')
    INSERT INTO Clinics (Id, Name, OpeningBalance, Created) VALUES (NEWID(), N'مركز بيوتي', 0, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Doctors WHERE Name = N'محمد الهادي')
    INSERT INTO Doctors (Id, Name, Created) VALUES (NEWID(), N'محمد الهادي', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Doctors WHERE Name = N'عبد السلام الاطرش')
    INSERT INTO Doctors (Id, Name, Created) VALUES (NEWID(), N'عبد السلام الاطرش', SYSUTCDATETIME());

-- Ti-Base: منتج موجود في الفواتير وغير موجود في قائمة الأسعار
IF NOT EXISTS (SELECT 1 FROM ServiceTypes WHERE Name = N'Ti-Base')
    INSERT INTO ServiceTypes (Id, Name, UnitPrice, UnitCost, IsActive, Created)
    VALUES (NEWID(), N'Ti-Base', 135, 0, 1, SYSUTCDATETIME());

DECLARE @ayar   uniqueidentifier = (SELECT Id FROM Clinics WHERE Name = N'مركز ايار');
DECLARE @eyab   uniqueidentifier = (SELECT Id FROM Clinics WHERE Name = N'مركز اياب');
DECLARE @beauty uniqueidentifier = (SELECT Id FROM Clinics WHERE Name = N'مركز بيوتي');

DECLARE @dHadi   uniqueidentifier = (SELECT Id FROM Doctors WHERE Name = N'محمد الهادي');
DECLARE @dBakr   uniqueidentifier = (SELECT Id FROM Doctors WHERE Name = N'ابو بكر الشريف');
DECLARE @dSalam  uniqueidentifier = (SELECT Id FROM Doctors WHERE Name = N'عبد السلام الاطرش');
DECLARE @dGharara uniqueidentifier = (SELECT Id FROM Doctors WHERE Name = N'احمد ابو غراره');
DECLARE @dKhuraiss uniqueidentifier = (SELECT Id FROM Doctors WHERE Name = N'احمد ابو خريص');

DECLARE @zfa   uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'ZIRCONIA FULL ANATOMY');
DECLARE @htz   uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'High Translucency Zirconia');
DECLARE @srz   uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'SCREW RETAINED ZIRCONIA');
DECLARE @tibase uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'Ti-Base');
DECLARE @model uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'3D MODEL UPPER / LOWER');
DECLARE @retainer uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'مثبت تقويم');
DECLARE @temp  uniqueidentifier = (SELECT Id FROM ServiceTypes WHERE Name = N'TEMPORARY CAD-CAM');

-- سعر خاص للطبيب احمد ابو غراره على الزيركون عالي الشفافية: 130 بدل 160
IF NOT EXISTS (SELECT 1 FROM DoctorPrices WHERE DoctorId = @dGharara AND ServiceTypeId = @htz)
    INSERT INTO DoctorPrices (Id, DoctorId, ServiceTypeId, Price, Notes, Created)
    VALUES (NEWID(), @dGharara, @htz, 130, N'حسب فاتورة مركز إياب شهر 8-2026', SYSUTCDATETIME());

DECLARE @norm nvarchar(20) = N'عادي';
DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @hardness nvarchar(60) = N'ZIRCONIA FULL ANATOMY - عالي الصلابة';

/* ================= مركز ايار ================= */

DECLARE @c282 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c282, 282, N'حمد مسعود', @dHadi, @ayar, '2026-08-03', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c282, @zfa, NULL, 1, 130, 0, 0, NULL, 0, @now),
 (NEWID(), @c282, @zfa, N'إعادة', 1, 0,   0, 1, N'كسر عند الطبيب', 1, @now),
 (NEWID(), @c282, @zfa, N'إعادة', 1, 130, 0, 1, N'تغيير بطلب الطبيب', 2, @now);

DECLARE @c284 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c284, 284, N'محمد عطية', @dHadi, @ayar, '2026-08-05', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c284, @zfa, NULL, 1, 130, 0, 0, NULL, 0, @now);

DECLARE @c294 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c294, 294, N'زيد منصور', @dBakr, @ayar, '2026-08-10', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c294, @zfa, NULL, 8, 130, 0, 0, NULL, 0, @now);

DECLARE @c296 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c296, 296, N'نادية هرة الهوني', @dSalam, @ayar, '2026-08-12', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c296, @zfa, NULL, 2, 130, 0, 0, NULL, 0, @now);

DECLARE @c306 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c306, 306, N'ربيعة الهادي', @dBakr, @ayar, '2026-08-20', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c306, @temp, N'مؤقت', 7, 40, 0, 0, NULL, 0, @now);

DECLARE @c308 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c308, 308, N'خديجة علي', @dBakr, @ayar, '2026-08-25', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c308, @zfa, NULL, 11, 130, 0, 0, NULL, 0, @now);

/* ================= مركز اياب ================= */

DECLARE @c279 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c279, 279, N'فاطمة', @dGharara, @eyab, '2026-08-02', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c279, @srz,    NULL, 3, 190, 0, 0, NULL, 0, @now),
 (NEWID(), @c279, @tibase, NULL, 3, 135, 0, 0, NULL, 0, @now);

DECLARE @c280 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c280, 280, N'رمزي', @dGharara, @eyab, '2026-08-02', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c280, @zfa, NULL, 6, 130, 0, 0, NULL, 0, @now);

DECLARE @c281 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c281, 281, N'فوزية ابو دربالة', @dGharara, @eyab, '2026-08-03', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c281, @zfa, @hardness, 1, 100, 0, 0, NULL, 0, @now);

DECLARE @c283 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c283, 283, N'تلاوة عبد الله', @dGharara, @eyab, '2026-08-04', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, ToothPosition, Created) VALUES
 (NEWID(), @c283, @htz, NULL, 6, 130, 0, 0, NULL, 0, N'upper', @now);

DECLARE @c286 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c286, 286, N'عبير احمد', @dGharara, @eyab, '2026-08-06', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c286, @srz, NULL,      1, 190, 0, 0, NULL, 0, @now),
 (NEWID(), @c286, @zfa, @hardness, 1, 100, 0, 0, NULL, 0, @now);

DECLARE @c288 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c288, 288, N'حنان', @dGharara, @eyab, '2026-08-08', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c288, @zfa, @hardness, 5, 100, 0, 0, NULL, 0, @now);

DECLARE @c289 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c289, 289, N'تلاوة عبد الله', @dGharara, @eyab, '2026-08-09', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, ToothPosition, Created) VALUES
 (NEWID(), @c289, @htz,   NULL, 6, 130, 0, 0, NULL, 0, N'lower', @now),
 (NEWID(), @c289, @model, NULL, 1, 50,  0, 0, NULL, 0, NULL,    @now);

DECLARE @c293 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c293, 293, N'فوزية', @dGharara, @eyab, '2026-08-11', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c293, @zfa, @hardness, 3, 100, 0, 0, NULL, 0, @now);

DECLARE @c297 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c297, 297, N'محمد المرغني', @dGharara, @eyab, '2026-08-13', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c297, @zfa,      @hardness,          1, 100, 0, 0, NULL, 0, @now),
 (NEWID(), @c297, @retainer, N'مثبت تقويم UPPER', 1, 50,  0, 0, NULL, 0, @now);

DECLARE @c298 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c298, 298, N'أيوب مي الدين', @dGharara, @eyab, '2026-08-14', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c298, @model, NULL, 1, 50, 0, 0, NULL, 0, @now);

DECLARE @c302 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c302, 302, N'ريهام', @dGharara, @eyab, '2026-08-18', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c302, @zfa, @hardness, 2, 100, 0, 0, NULL, 0, @now);

DECLARE @c307 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c307, 307, N'الهام أبو غرارة', @dGharara, @eyab, '2026-08-23', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c307, @zfa, @hardness, 11, 100, 0, 0, NULL, 0, @now);

/* ================= مركز بيوتي ================= */

DECLARE @c276 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c276, 276, N'يسري سالم', @dKhuraiss, @beauty, '2026-08-02', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c276, @zfa, NULL, 1, 130, 0, 0, NULL, 0, @now);

DECLARE @c295 uniqueidentifier = NEWID();
INSERT INTO LabCases (Id, CaseNumber, PatientName, DoctorId, ClinicId, ReceivedDate, Year, Month, Status, Created)
VALUES (@c295, 295, N'امنة حسين', @dKhuraiss, @beauty, '2026-08-12', 2026, 8, @norm, @now);
INSERT INTO LabCaseItems (Id, LabCaseId, ServiceTypeId, ProductName, Quantity, UnitPrice, UnitCost, IsRemake, RemakeReason, RemakeSequence, Created) VALUES
 (NEWID(), @c295, @zfa, NULL, 1, 130, 0, 0, NULL, 0, @now);

COMMIT TRAN;
SELECT 'seeded august cases=' + CONVERT(varchar, (SELECT COUNT(*) FROM LabCases WHERE Year=2026 AND Month=8));
