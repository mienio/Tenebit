using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationEntityMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: tenebit.asset_locations (table + IX_asset_locations_OrganizationId_ParentId index) already
            // exists - created by raw SQL in InitialCreate because the table had no EF entity mapping back then.
            // This migration only brings the EF model snapshot in sync now that Location is a mapped entity
            // (audit P2 #2: moved location logic out of raw SQL / Api-layer DbContext use into Application).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op - see Up().
        }
    }
}
