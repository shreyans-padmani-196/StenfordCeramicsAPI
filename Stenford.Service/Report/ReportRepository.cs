using Stenford.Domain;
using Stenford.Domain.DataContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Stenford.Domain.DTO;

namespace Stenford.Service.Report
{
    public class ReportRepository : IReportRepository
    {

        private readonly ApplicationDbContext _context;

        public ReportRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public ReportDTO GetReport(int? showroomId, int? cityId, DateTime? fromDate, DateTime? toDate, int pageIndex, int pageSize)
        {
            try
            {
                List<ReportRowDTO> reportObj = (from v in _context.VisVisits
                                                join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
                                                join city in _context.LocCities on showroom.CityId equals city.CityId into cityJoin
                                                from city in cityJoin.DefaultIfEmpty()
                                                where v.IsDeleted != true && showroom.IsDeleted != true &&
                                                (!showroomId.HasValue || v.ShowroomId == showroomId) &&
                                                (!cityId.HasValue || showroom.CityId == cityId) &&
                                                (!fromDate.HasValue || v.VisitDate >= fromDate) &&
                                                (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
                                                group v by new { showroom.ShowroomId, showroom.ShowroomName, city.CityName } into g
                                                select new ReportRowDTO
                                                {
                                                    ShowroomName = g.Key.ShowroomName,
                                                    City = g.Key.CityName,
                                                    VisitCount = g.Count(),
                                                    LastVisit = g.Max(x => x.VisitDate)
                                                }).OrderByDescending(r => r.VisitCount).ToList();

                if (reportObj.Any())
                {
                    var totalRecords = reportObj.Count;
                    reportObj = reportObj.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                    reportObj.First().TotalRecords = totalRecords;
                }

                var totalVisits = (from v in _context.VisVisits
                                   join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
                                   where v.IsDeleted != true && showroom.IsDeleted != true &&
                                   (!showroomId.HasValue || v.ShowroomId == showroomId) &&
                                   (!cityId.HasValue || showroom.CityId == cityId) &&
                                   (!fromDate.HasValue || v.VisitDate >= fromDate) &&
                                   (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
                                   select v).Count();

                var showroomCount = (from v in _context.VisVisits
                                     join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
                                     where v.IsDeleted != true && showroom.IsDeleted != true &&
                                     (!showroomId.HasValue || v.ShowroomId == showroomId) &&
                                     (!cityId.HasValue || showroom.CityId == cityId) &&
                                     (!fromDate.HasValue || v.VisitDate >= fromDate) &&
                                     (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
                                     select v.ShowroomId).Distinct().Count();

                var stateCount = (from v in _context.VisVisits
                                  join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
                                  where v.IsDeleted != true && showroom.IsDeleted != true &&
                                  (!showroomId.HasValue || v.ShowroomId == showroomId) &&
                                  (!cityId.HasValue || showroom.CityId == cityId) &&
                                  (!fromDate.HasValue || v.VisitDate >= fromDate) &&
                                  (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
                                  select showroom.StateId).Distinct().Count();

                // Step 1: Figure out the START date
                DateTime startDate;
                if (fromDate.HasValue)
                {
                    startDate = fromDate.Value;
                }
                else
                {
                    startDate = _context.VisVisits.Min(v => v.VisitDate);
                }

                // Step 2: Figure out the END date
                DateTime endDate;
                if (toDate.HasValue)
                {
                    endDate = toDate.Value;
                }
                else
                {
                    endDate = DateTime.Now;
                }

                // Step 3: Count how many days are between start and end (inclusive)
                int totalDays = (endDate.Date - startDate.Date).Days + 1;

                // Step 4: Calculate the average, but avoid dividing by zero
                double avgPerDay = 0;
                if (totalDays > 0)
                {
                    avgPerDay = (double)totalVisits / totalDays;
                    avgPerDay = Math.Round(avgPerDay, 1);
                }

                return new ReportDTO
                {
                    TotalVisits = totalVisits,
                    ShowroomCount = showroomCount,
                    StateCount = stateCount,
                    AvgPerDay = avgPerDay,
                    VisitReport = reportObj
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
