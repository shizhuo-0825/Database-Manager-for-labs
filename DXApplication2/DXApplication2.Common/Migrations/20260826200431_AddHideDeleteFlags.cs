using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXApplication2.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddHideDeleteFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPlotTemplate = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderPath = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Material = table.Column<string>(type: "TEXT", nullable: true),
                    ExperimentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CsvPath = table.Column<string>(type: "TEXT", nullable: true),
                    CsvHash = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Extras = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPlotTemplate = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionMembers_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionMembers_DataGroups_DataGroupId",
                        column: x => x.DataGroupId,
                        principalTable: "DataGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordName = table.Column<string>(type: "TEXT", nullable: true),
                    RowIndex = table.Column<string>(type: "TEXT", nullable: true),
                    ImageFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    ResultQuantity = table.Column<double>(type: "REAL", nullable: true),
                    ResultPath = table.Column<string>(type: "TEXT", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExptParams = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataRecords_DataGroups_DataGroupId",
                        column: x => x.DataGroupId,
                        principalTable: "DataGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentParamss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FieldName = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    DataType = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExperimentTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentParamss", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperimentParamss_ExperimentTypes_ExperimentTypeId",
                        column: x => x.ExperimentTypeId,
                        principalTable: "ExperimentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupExperimentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExperimentTypeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExperimentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExperimentTypes_DataGroups_DataGroupId",
                        column: x => x.DataGroupId,
                        principalTable: "DataGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupExperimentTypes_ExperimentTypes_ExperimentTypeId",
                        column: x => x.ExperimentTypeId,
                        principalTable: "ExperimentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisParamss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HashParamsPath = table.Column<string>(type: "TEXT", nullable: true),
                    DataRecordId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisParamss", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisParamss_DataRecords_DataRecordId",
                        column: x => x.DataRecordId,
                        principalTable: "DataRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisParamss_DataRecordId",
                table: "AnalysisParamss",
                column: "DataRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMembers_CollectionId",
                table: "CollectionMembers",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMembers_DataGroupId",
                table: "CollectionMembers",
                column: "DataGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DataFolders_FolderPath",
                table: "DataFolders",
                column: "FolderPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataRecords_DataGroupId",
                table: "DataRecords",
                column: "DataGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentParamss_ExperimentTypeId",
                table: "ExperimentParamss",
                column: "ExperimentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentParamss_FieldName",
                table: "ExperimentParamss",
                column: "FieldName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentTypes_Name",
                table: "ExperimentTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExperimentTypes_DataGroupId",
                table: "GroupExperimentTypes",
                column: "DataGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExperimentTypes_ExperimentTypeId",
                table: "GroupExperimentTypes",
                column: "ExperimentTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisParamss");

            migrationBuilder.DropTable(
                name: "CollectionMembers");

            migrationBuilder.DropTable(
                name: "DataFolders");

            migrationBuilder.DropTable(
                name: "ExperimentParamss");

            migrationBuilder.DropTable(
                name: "GroupExperimentTypes");

            migrationBuilder.DropTable(
                name: "DataRecords");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "ExperimentTypes");

            migrationBuilder.DropTable(
                name: "DataGroups");
        }
    }
}
