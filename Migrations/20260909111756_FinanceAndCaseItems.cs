using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAndCaseItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabCases_ServiceTypes_ServiceTypeId",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "CreditAmount",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "DebitAmount",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "InvoicePrice",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "WorkDescription",
                table: "LabCases");

            migrationBuilder.RenameColumn(
                name: "ServiceTypeId",
                table: "LabCases",
                newName: "InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_LabCases_ServiceTypeId",
                table: "LabCases",
                newName: "IX_LabCases_InvoiceId");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                table: "SiteInfo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "SiteInfo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "SiteInfo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerName",
                table: "SiteInfo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerSharePercent",
                table: "SiteInfo",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "SiteInfo",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ServiceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "ServiceTypes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                table: "Clinics",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DoctorPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorPrices_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoctorPrices_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsCapital = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Number = table.Column<int>(type: "int", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabCaseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsRemake = table.Column<bool>(type: "bit", nullable: false),
                    RemakeReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RemakeSequence = table.Column<int>(type: "int", nullable: false),
                    ToothPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCaseItems_LabCases_LabCaseId",
                        column: x => x.LabCaseId,
                        principalTable: "LabCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabCaseItems_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Expenses_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoanTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsWrittenOff = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanTransactions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanTransactions_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IncomeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsReceived = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeEntries_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IncomeEntries_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IncomeEntries_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseNumber = table.Column<int>(type: "int", nullable: true),
                    PatientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DoctorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsRemake = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ExpenseCategories",
                columns: new[] { "Id", "Created", "IsCapital", "Modified", "Name", "Notes" },
                values: new object[,]
                {
                    { new Guid("1a000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "مواد ومستلزمات", null },
                    { new Guid("1a000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "إيجار", null },
                    { new Guid("1a000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "رواتب ومكافآت", null },
                    { new Guid("1a000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "صيانة", null },
                    { new Guid("1a000000-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "عمولات مصرفية", null },
                    { new Guid("1a000000-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "معامل خارجية", null },
                    { new Guid("1a000000-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "نقل وشحن", null },
                    { new Guid("1a000000-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "نت وخدمات", null },
                    { new Guid("1a000000-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "نسبة شريك", null },
                    { new Guid("1a000000-0000-0000-0000-00000000000a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "أجهزة ومعدات", null },
                    { new Guid("1a000000-0000-0000-0000-00000000000b"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, "أخرى", null }
                });

            migrationBuilder.InsertData(
                table: "FinancialAccounts",
                columns: new[] { "Id", "Created", "IsActive", "Kind", "Modified", "Name", "Notes", "OpeningBalance" },
                values: new object[,]
                {
                    { new Guid("2b000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "نقدي", null, "كاش المعمل", null, 0m },
                    { new Guid("2b000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "مصرفي", null, "حساب عبدو", null, 0m },
                    { new Guid("2b000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "مصرفي", null, "حساب عادل", null, 0m },
                    { new Guid("2b000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "مصرفي", null, "حساب شمال أفريقيا", null, 0m }
                });

            migrationBuilder.UpdateData(
                table: "SiteInfo",
                keyColumn: "Id",
                keyValue: new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
                columns: new[] { "BankAccountName", "BankAccountNumber", "BankName", "PartnerName", "PartnerSharePercent", "Phone" },
                values: new object[] { "محمد نجم الدين الطاهر الفتحي", "LY07007014014011428015012", "مصرف شمال أفريقيا", null, 0m, "091-9883982" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPrices_DoctorId_ServiceTypeId",
                table: "DoctorPrices",
                columns: new[] { "DoctorId", "ServiceTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorPrices_ServiceTypeId",
                table: "DoctorPrices",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId",
                table: "Expenses",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_FinancialAccountId",
                table: "Expenses",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Year_Month",
                table: "Expenses",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_ClinicId",
                table: "IncomeEntries",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_FinancialAccountId",
                table: "IncomeEntries",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_InvoiceId",
                table: "IncomeEntries",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_Year_Month",
                table: "IncomeEntries",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClinicId",
                table: "Invoices",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DoctorId",
                table: "Invoices",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseItems_LabCaseId",
                table: "LabCaseItems",
                column: "LabCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseItems_ServiceTypeId",
                table: "LabCaseItems",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanTransactions_EmployeeId",
                table: "LoanTransactions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanTransactions_FinancialAccountId",
                table: "LoanTransactions",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanTransactions_Year_Month",
                table: "LoanTransactions",
                columns: new[] { "Year", "Month" });

            migrationBuilder.AddForeignKey(
                name: "FK_LabCases_Invoices_InvoiceId",
                table: "LabCases",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabCases_Invoices_InvoiceId",
                table: "LabCases");

            migrationBuilder.DropTable(
                name: "DoctorPrices");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "IncomeEntries");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "LabCaseItems");

            migrationBuilder.DropTable(
                name: "LoanTransactions");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "PartnerName",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "PartnerSharePercent",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "SiteInfo");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ServiceTypes");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "ServiceTypes");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                table: "Clinics");

            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "LabCases",
                newName: "ServiceTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_LabCases_InvoiceId",
                table: "LabCases",
                newName: "IX_LabCases_ServiceTypeId");

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmount",
                table: "LabCases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DebitAmount",
                table: "LabCases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoicePrice",
                table: "LabCases",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "LabCases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WorkDescription",
                table: "LabCases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabCases_ServiceTypes_ServiceTypeId",
                table: "LabCases",
                column: "ServiceTypeId",
                principalTable: "ServiceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
