using Stenford.Domain;
using Stenford.Domain.DataContext;
using Stenford.Domain.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniqueITAdminPanel.Common.Utility;
using static Stenford.Common.Constants.Enums;
using static Stenford.Domain.DTO;

namespace Stenford.Service.Visit
{
	public class VisitRepository : IVisitRepository
	{
		private readonly ApplicationDbContext _context;

		public VisitRepository(ApplicationDbContext context)
		{
			_context = context;
		}


		public List<VisitDTO> GetVisitList(int pageIndex, int pageSize, int? stateId, int? cityId, int? salesPersonId, DateTime? fromDate, DateTime? toDate)
		{
			try
			{
				List<VisitDTO> visitObj = (from v in _context.VisVisits
											   join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
											   join sp in _context.SecSalesPeople on v.SalesPersonId equals sp.SalesPersonId
											   join city in _context.LocCities on showroom.CityId equals city.CityId into cityJoin
											   from city in cityJoin.DefaultIfEmpty()
											   where v.IsDeleted != true  && 
											   (!stateId.HasValue || showroom.StateId == stateId) &&
											   (!cityId.HasValue || showroom.CityId == cityId) &&
											   (!salesPersonId.HasValue || v.SalesPersonId == salesPersonId) &&
											   (!fromDate.HasValue || v.VisitDate >= fromDate) &&
											   (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
											   orderby v.VisitDate descending
											   select new VisitDTO
											   {
												   VisitId = v.VisitId,
												   ShowroomName = showroom.ShowroomName,
												   Location = city.CityName,
												   SalesPersonId = sp.SalesPersonId,
												   SalesPersonName = sp.SalesPersonName,
												   VisitDate = v.VisitDate,
												   DiscussionNotes = v.DiscussionNotes,
												   //Products = v.ProductsDiscussedString.Split("@#$%^&**&^%$#@", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
												   ShowroomId = v.ShowroomId,

											   }).ToList();

				if (visitObj.Any())
				{
					var totalRecords = visitObj.Count;
					visitObj = visitObj.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
					visitObj.First().TotalRecords = totalRecords;
				}

				return visitObj;
			}
			catch
			{
				return null;
			}
		}

		public VisitDTO GetVisitById(int visitId)
		{
			var visit = (from v in _context.VisVisits
						 join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
						 join sp in _context.SecSalesPeople on v.SalesPersonId equals sp.SalesPersonId
						 join city in _context.LocCities on showroom.CityId equals city.CityId into cityJoin
						 from city in cityJoin.DefaultIfEmpty()
						 join state in _context.LocStates on showroom.StateId equals state.StateId into stateJoin
						 from state in stateJoin.DefaultIfEmpty()
						 where v.VisitId == visitId && v.IsDeleted == false
						 select new VisitDTO	
						 {
							 VisitId = v.VisitId,
							 ShowroomId = v.ShowroomId,
							 ShowroomName = showroom.ShowroomName,
							 Location = city.CityName + ", " + state.StateName,
							 SalesPersonId = sp.SalesPersonId,
							 SalesPersonName = sp.SalesPersonName,
							 VisitDate = v.VisitDate,
							 DiscussionNotes = v.DiscussionNotes,
                             //Products = v.ProductsDiscussedString.Split("@#$%^&**&^%$#@", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                             //Latitude = v.Latitude,
                             //Longitude = v.Longitude,
                             Latitude = showroom.Latitude ?? v.Latitude,
                             Longitude = showroom.Longitude ?? v.Longitude,
                             VoiceNotePath = CommonHelper.GetFullImagePath(v.VoiceNotePath),
							 VisitingCardFrontPath = CommonHelper.GetFullImagePath(v.VisitingCardFrontPath),
							 VisitingCardBackPath = CommonHelper.GetFullImagePath(v.VisitingCardBackPath),
						 }).FirstOrDefault();

			if (visit == null)
			{
				return null;
			}

			var attachments = _context.VisVisitWiseAttachments
				.Where(a => a.VisitId == visitId && a.IsDeleted == false)
				.ToList();

			//visit.VoiceNoteUrl = attachments.FirstOrDefault(a => a.AttachmentType == (int)AttachmentType.VoiceNote)?.AttachmentPath;
			//visit.VisitingCardImages = attachments.Where(a => a.AttachmentType == (int)AttachmentType.VisitingCard).Select(a => a.AttachmentPath).ToList();
			visit.ShowroomImages = attachments.Where(a => a.AttachmentType == (int)AttachmentType.ShowroomImage).Select(a => CommonHelper.GetFullImagePath(a.AttachmentPath)).ToList();

			return visit;
		}

		public VisitMapDTO GetVisitMapPoints(int? stateId, int? cityId, int? salesPersonId, DateTime? fromDate, DateTime? toDate)
		{
			try
			{
				// Get all visits that match the filters, along with each salesperson's info
				var matchingVisits = (from v in _context.VisVisits
									  join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
									  join sp in _context.SecSalesPeople on v.SalesPersonId equals sp.SalesPersonId
									  where v.IsDeleted != true &&
											
											(!stateId.HasValue || showroom.StateId == stateId) &&
											(!cityId.HasValue || showroom.CityId == cityId) &&
											(!salesPersonId.HasValue || v.SalesPersonId == salesPersonId) &&
											(!fromDate.HasValue || v.VisitDate >= fromDate) &&
											(!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
									  select new VisitMapPointDTO
									  {
										  VisitId = v.VisitId,
                                          //Latitude = v.Latitude,
                                          //Longitude = v.Longitude,
                                          Latitude = showroom.Latitude ?? v.Latitude,      // 👈 Use showroom location first
                                          Longitude = showroom.Longitude ?? v.Longitude,
                                          SalesPersonId = sp.SalesPersonId,
										  SalesPersonName = sp.SalesPersonName
									  }).ToList();

				// For every salesperson (even ones with 0 matching visits), count their visits
				var salesPersonCounts = _context.SecSalesPeople
					.ToList()
					.Select(sp => new SalesPersonVisitCountDTO
					{
						SalesPersonId = sp.SalesPersonId,
						SalesPersonName = sp.SalesPersonName,
						VisitCount = matchingVisits.Count(v => v.SalesPersonId == sp.SalesPersonId)
					}).OrderByDescending(sp => sp.VisitCount).ToList();

				return new VisitMapDTO
				{
					Points = matchingVisits,
					SalesPersonCounts = salesPersonCounts
				};
			}
			catch
			{
				return null;
			}
		}

        //public VisitDTO AddVisit(VisitDTO visitDTO, Guid aspnetUserId)
        //{
        //	VisVisit visit = new VisVisit();
        //	visit.ShowroomId = visitDTO.ShowroomId;
        //	visit.SalesPersonId = visitDTO.SalesPersonId;
        //	visit.VisitDate = visitDTO.VisitDate;
        //	visit.Latitude = visitDTO.Latitude;
        //	visit.Longitude = visitDTO.Longitude;
        //	visit.DiscussionNotes = visitDTO.DiscussionNotes;

        //	visit.ProductsDiscussedString = string.Join("@#$%^&**&^%$#@", visitDTO.Products);
        //	visit.VoiceNotePath = visitDTO.VoiceNotePath;
        //	visit.VisitingCardFrontPath = visitDTO.VisitingCardFrontPath;
        //	visit.VisitingCardBackPath = visitDTO.VisitingCardBackPath;
        //	visit.IsDeleted = false;
        //	visit.CreatedAt = DateTime.Now;
        //	visit.CreatedBy = aspnetUserId;
        //	visit.ModifiedAt = DateTime.Now;
        //	visit.ModifiedBy = aspnetUserId;

        //	foreach (var imagePath in visitDTO.ShowroomImages)
        //	{
        //		VisVisitWiseAttachment attachment = new VisVisitWiseAttachment();
        //		attachment.AttachmentType = (int)AttachmentType.ShowroomImage;
        //		attachment.AttachmentPath = imagePath;
        //		attachment.IsDeleted = false;
        //		attachment.CreatedAt = DateTime.Now;
        //		attachment.CreatedBy = aspnetUserId;
        //		attachment.ModifiedAt = DateTime.Now;
        //		attachment.ModifiedBy = aspnetUserId;
        //		visit.VisVisitWiseAttachments.Add(attachment);
        //	}

        //	_context.VisVisits.Add(visit);
        //	_context.SaveChanges();
        //	visitDTO.VisitId = visit.VisitId;
        //	return visitDTO;
        //}

        //public DTO.VisitAddDTO AddVisit(DTO.VisitAddDTO visitDTO, int salesPersonId, Guid aspnetUserId)
        //{
        //	VisVisit visit = new VisVisit();
        //	visit.ShowroomId = visitDTO.ShowroomId;
        //	visit.SalesPersonId = salesPersonId;
        //	visit.VisitDate = visitDTO.VisitDate;
        //	visit.Latitude = visitDTO.Latitude;
        //	visit.Longitude = visitDTO.Longitude;
        //	visit.DiscussionNotes = visitDTO.DiscussionNotes;
        //	visit.ProductsDiscussedString = string.Join("@#$%^&**&^%$#@", visitDTO.Products);
        //	visit.IsDeleted = false;
        //	visit.CreatedAt = DateTime.Now;
        //	visit.CreatedBy = aspnetUserId;
        //	visit.ModifiedAt = DateTime.Now;
        //	visit.ModifiedBy = aspnetUserId;

        //	_context.VisVisits.Add(visit);
        //	_context.SaveChanges();

        //	visitDTO.VisitId = visit.VisitId;
        //	return visitDTO;
        //}

        public DTO.VisitAddDTO AddVisit(DTO.VisitAddDTO visitDTO, Guid aspnetUserId)
        {
            var salesPerson = _context.SecSalesPeople.FirstOrDefault(sp => sp.AspNetUserId == aspnetUserId && sp.IsDeleted == false);

            if (salesPerson == null)
            {
                return null; // no salesperson found for this login — reject
            }

            VisVisit visit = new VisVisit();
            visit.ShowroomId = visitDTO.ShowroomId;
            visit.SalesPersonId = salesPerson.SalesPersonId;  // ← looked up, not hardcoded or passed separately
            visit.VisitDate = visitDTO.VisitDate;
            visit.Latitude = visitDTO.Latitude;
            visit.Longitude = visitDTO.Longitude;
            visit.DiscussionNotes = visitDTO.DiscussionNotes;
            //visit.ProductsDiscussedString = string.Join("@#$%^&**&^%$#@", visitDTO.Products);
            visit.VoiceNotePath = visitDTO.VoiceNotePath;
            visit.VisitingCardFrontPath = visitDTO.VisitingCardFrontPath;
            visit.VisitingCardBackPath = visitDTO.VisitingCardBackPath;
            visit.IsDeleted = false;
            visit.CreatedAt = DateTime.Now;
            visit.CreatedBy = aspnetUserId;
            visit.ModifiedAt = DateTime.Now;
            visit.ModifiedBy = aspnetUserId;

            _context.VisVisits.Add(visit);
            _context.SaveChanges();

            foreach (var imagePath in visitDTO.ShowroomImages)
            {
                VisVisitWiseAttachment attachment = new VisVisitWiseAttachment();
                attachment.VisitId = visit.VisitId;
                attachment.AttachmentType = (int)AttachmentType.ShowroomImage;
                attachment.AttachmentPath = imagePath;
                attachment.IsDeleted = false;
                attachment.CreatedAt = DateTime.Now;
                attachment.CreatedBy = aspnetUserId;
                attachment.ModifiedAt = DateTime.Now;
                attachment.ModifiedBy = aspnetUserId;
                _context.VisVisitWiseAttachments.Add(attachment);
            }
            _context.SaveChanges();

            visitDTO.VisitId = visit.VisitId;
            visitDTO.SalesPersonId = salesPerson.SalesPersonId;
            return visitDTO;
        }
        public List<VisitDTO> GetVisitHistoryList(Guid aspNetUserId, int pageIndex, int pageSize, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var salesPerson = _context.SecSalesPeople.FirstOrDefault(sp => sp.AspNetUserId == aspNetUserId && sp.IsDeleted == false);

                if (salesPerson == null)
                {
                    return null;
                }

                int salesPersonId = salesPerson.SalesPersonId;

                List<VisitDTO> visitObj = (from v in _context.VisVisits
                                           join showroom in _context.ShoShowrooms on v.ShowroomId equals showroom.ShowroomId
                                           join sp in _context.SecSalesPeople on v.SalesPersonId equals sp.SalesPersonId
                                           where v.IsDeleted != true &&
                                           v.SalesPersonId == salesPersonId &&
                                           (!fromDate.HasValue || v.VisitDate >= fromDate) &&
                                           (!toDate.HasValue || v.VisitDate < toDate.Value.Date.AddDays(1))
                                           orderby v.VisitDate descending
                                           select new VisitDTO
                                           {
                                               VisitId = v.VisitId,
                                               ShowroomId = v.ShowroomId,
                                               ShowroomName = showroom.ShowroomName,
                                               SalesPersonId = sp.SalesPersonId,
                                               SalesPersonName = sp.SalesPersonName,
                                               VisitDate = v.VisitDate,
                                               DiscussionNotes = v.DiscussionNotes,
                                               //Products = v.ProductsDiscussedString.Split("@#$%^&**&^%$#@", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                                               VoiceNotePath = CommonHelper.GetFullImagePath(v.VoiceNotePath),
                                               VisitingCardFrontPath = CommonHelper.GetFullImagePath(v.VisitingCardFrontPath),
                                               VisitingCardBackPath = CommonHelper.GetFullImagePath(v.VisitingCardBackPath)
                                           }).ToList();

                if (visitObj.Any())
                {
                    var totalRecords = visitObj.Count;
                    visitObj = visitObj.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                    var visitIds = visitObj.Select(x => x.VisitId).ToList();
                    var showroomImages = _context.VisVisitWiseAttachments
                        .Where(a => visitIds.Contains(a.VisitId) && a.IsDeleted != true && a.AttachmentType == (int)AttachmentType.ShowroomImage)
                        .ToList();
                    foreach (var visit in visitObj)
                    {
                        visit.ShowroomImages = showroomImages.Where(a => a.VisitId == visit.VisitId).Select(a => CommonHelper.GetFullImagePath(a.AttachmentPath)).ToList();
                    }
                    visitObj.First().TotalRecords = totalRecords;
                }
                return visitObj;
            }
            catch
            {
                return null;
            }
        }
    }
}
