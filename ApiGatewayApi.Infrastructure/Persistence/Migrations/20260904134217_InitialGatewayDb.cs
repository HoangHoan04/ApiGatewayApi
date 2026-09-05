using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiGatewayApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGatewayDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cors_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AllowedOriginsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AllowCredentials = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cors_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gateway_services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HealthPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Icon = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rate_limit_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestsPerMinute = table.Column<int>(type: "integer", nullable: true),
                    RequestsPerDay = table.Column<int>(type: "integer", nullable: true),
                    Burst = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limit_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "request_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QueryString = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    TargetCluster = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestSize = table.Column<long>(type: "bigint", nullable: true),
                    ResponseSize = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gateway_clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClusterId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LoadBalancing = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    CircuitBreakerEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CircuitBreakerFailures = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gateway_clusters_gateway_services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "gateway_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_windows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_windows_gateway_services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "gateway_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gateway_destinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    HealthStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastHealthAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_destinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gateway_destinations_gateway_clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "gateway_clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gateway_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateLimitPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorsPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PathMatch = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MethodsJson = table.Column<string>(type: "jsonb", nullable: true),
                    TransformsJson = table.Column<string>(type: "jsonb", nullable: true),
                    AuthorizationPolicy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gateway_routes_cors_policies_CorsPolicyId",
                        column: x => x.CorsPolicyId,
                        principalTable: "cors_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_gateway_routes_gateway_clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "gateway_clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gateway_routes_rate_limit_policies_RateLimitPolicyId",
                        column: x => x.RateLimitPolicyId,
                        principalTable: "rate_limit_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gateway_alert_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    WindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    WebhookUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_alert_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gateway_alert_rules_gateway_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "gateway_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gateway_alert_rules_gateway_services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "gateway_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ip_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cidr = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ip_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ip_rules_gateway_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "gateway_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cors_policies_Name",
                table: "cors_policies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gateway_alert_rules_RouteId",
                table: "gateway_alert_rules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_alert_rules_ServiceId",
                table: "gateway_alert_rules",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_clusters_ClusterId",
                table: "gateway_clusters",
                column: "ClusterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gateway_clusters_ServiceId",
                table: "gateway_clusters",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_destinations_ClusterId",
                table: "gateway_destinations",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_routes_ClusterId",
                table: "gateway_routes",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_routes_CorsPolicyId",
                table: "gateway_routes",
                column: "CorsPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_routes_RateLimitPolicyId",
                table: "gateway_routes",
                column: "RateLimitPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_gateway_routes_RouteId",
                table: "gateway_routes",
                column: "RouteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gateway_services_Code",
                table: "gateway_services",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ip_rules_RouteId",
                table: "ip_rules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_ServiceId",
                table: "maintenance_windows",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_rate_limit_policies_Name",
                table: "rate_limit_policies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_CorrelationId",
                table: "request_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_CreatedAt",
                table: "request_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_TargetCluster",
                table: "request_logs",
                column: "TargetCluster");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gateway_alert_rules");

            migrationBuilder.DropTable(
                name: "gateway_destinations");

            migrationBuilder.DropTable(
                name: "ip_rules");

            migrationBuilder.DropTable(
                name: "maintenance_windows");

            migrationBuilder.DropTable(
                name: "request_logs");

            migrationBuilder.DropTable(
                name: "gateway_routes");

            migrationBuilder.DropTable(
                name: "cors_policies");

            migrationBuilder.DropTable(
                name: "gateway_clusters");

            migrationBuilder.DropTable(
                name: "rate_limit_policies");

            migrationBuilder.DropTable(
                name: "gateway_services");
        }
    }
}
