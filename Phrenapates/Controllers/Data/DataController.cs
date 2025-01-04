using Microsoft.AspNetCore.Mvc;
using Plana.Database;
using Phrenapates.Services;
using Plana.FlatData;

namespace Phrenapates.Controllers.Data
{
    [ApiController]
    [Route("/data")]
    public class DataController : ControllerBase
    {
        private readonly SCHALEContext context;
        private readonly ExcelTableService excelTableService;

        public DataController(SCHALEContext _context, ExcelTableService _excelTableService)
        {
            context = _context;
            excelTableService = _excelTableService;
        }

        [Route("content")]
        public ContentResult Raid()
        {
            var raidData = excelTableService.GetTable<RaidSeasonManageExcelTable>().UnPack().DataList;
            var timeAttackDungeonData = excelTableService.GetTable<TimeAttackDungeonSeasonManageExcelTable>().UnPack().DataList;
            var eliminateRaidData = excelTableService.GetTable<EliminateRaidSeasonManageExcelTable>().UnPack().DataList;
            var TADExcel = excelTableService.GetTable<TimeAttackDungeonExcelTable>().UnPack().DataList;

            var html = @"
                <table border='1'>
                    <tr>
                        <th>Raid Id</th>
                        <th>Boss Detail</th>
                        <th>Date</th>
                        <th>Time Attack Dungeon Id</th>
                        <th>JFD Type</th>
                        <th>Date</th>
                        <th>Eliminate Raid Id</th>
                        <th>Boss Detail</th>
                        <th>Date</th>
                    </tr>";

            // Determine the maximum number of rows needed
            int maxRows = Math.Max(raidData.Count, Math.Max(timeAttackDungeonData.Count, eliminateRaidData.Count));

            for (int i = 0; i < maxRows; i++)
            {
                html += "<tr>";

                // Raid Data
                if (i < raidData.Count)
                {
                    var raid = raidData[i];
                    html += $@"
                        <td>{raid.SeasonId}</td>
                        <td>{raid.OpenRaidBossGroup.FirstOrDefault() ?? "N/A"}</td>
                        <td>{raid.SeasonStartData:yyyy-MM-dd}</td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                // Time Attack Dungeon Data
                if (i < timeAttackDungeonData.Count)
                {
                    
                    var dungeon = timeAttackDungeonData[i];
                    html += $@"
                        <td>{dungeon.Id}</td>
                        <td>{TADExcel.FirstOrDefault(x => x.Id == dungeon.DungeonId).TimeAttackDungeonType}</td>
                        <td>{dungeon.StartDate:yyyy-MM-dd}</td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                // Eliminate Raid Data
                if (i < eliminateRaidData.Count)
                {
                    var eliminate = eliminateRaidData[i];
                    var groupedBosses = new List<string>
                    {
                        eliminate.OpenRaidBossGroup01,
                        eliminate.OpenRaidBossGroup02,
                        eliminate.OpenRaidBossGroup03
                    }
                    .Where(boss => !string.IsNullOrEmpty(boss))
                    .Select(boss => boss.Split('_'))
                    .GroupBy(parts => parts[0])
                    .Select(group => $"{group.Key} {group.Select(parts => parts[1]).First()} ({string.Join(", ", group.Select(parts => parts[2]))})")
                    .ToList();

                    html += $@"
                        <td>{eliminate.SeasonId}</td>
                        <td>{string.Join(", ", groupedBosses)}</td>
                        <td>{eliminate.SeasonStartData:yyyy-MM-dd}</td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                html += "</tr>";
            }

            html += "</table>";

            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }

    }
}
