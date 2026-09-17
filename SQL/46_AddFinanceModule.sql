-- ============================================================
-- 46_AddFinanceModule.sql
-- Add the Finance module as a real, assignable permission — replaces the previous
-- hardcoded UserId==10-only gate on the Finance dashboard (RequireFinanceAdminAttribute /
-- ApiRequireFinanceAdminAttribute), which only ever worked for one specific developer account
-- and breaks for every other customer this app is deployed to.
-- ============================================================
USE SchoolManagementDB;
GO

IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'finance_view')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Finance Dashboard', 'finance_view', 'fas fa-chart-pie', 'Records', 110, 1);
END

PRINT '✅ Finance module added.';
GO
