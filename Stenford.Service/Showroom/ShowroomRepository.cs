using Stenford.Common.Utility;
using Stenford.Domain;
using Stenford.Domain.DataContext;
using Stenford.Domain.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniqueITAdminPanel.Common.Utility;
using static Stenford.Domain.DTO;

namespace Stenford.Service.Showroom
{
	public class ShowroomRepository : IShowroomRepository
	{
		private readonly ApplicationDbContext _context;
		public ShowroomRepository(ApplicationDbContext context) 
		{
			_context = context;
		}

        public List<ShowroomDTO> GetShowroomDataList(string searchText,int pageIndex,int pageSize,int? stateId,int? cityId,Guid aspNetUserId)
        {
            try
            {
                var salesPerson = _context.SecSalesPeople
                    .FirstOrDefault(sp =>
                        sp.AspNetUserId == aspNetUserId &&
                        sp.IsDeleted == false);

                List<ShowroomDTO> showroomObj =
                    (from showroom in _context.ShoShowrooms

                     join state in _context.LocStates
                         on showroom.StateId equals state.StateId into stateJoin
                     from state in stateJoin.DefaultIfEmpty()

                     join city in _context.LocCities
                         on showroom.CityId equals city.CityId into cityJoin
                     from city in cityJoin.DefaultIfEmpty()

                     where showroom.IsDeleted != true
                           && showroom.CountryId == 101
                           && (salesPerson == null || showroom.CreatedBy == aspNetUserId)
                           && (!stateId.HasValue || showroom.StateId == stateId)
                           && (!cityId.HasValue || showroom.CityId == cityId)
                           && (
                               string.IsNullOrEmpty(searchText)
                               || showroom.ShowroomName.ToLower().Contains(searchText.ToLower())
                               || showroom.DealerName.ToLower().Contains(searchText.ToLower())
                               || showroom.PrimaryContact.ToLower().Contains(searchText.ToLower())
                               || (city != null && city.CityName.ToLower().Contains(searchText.ToLower()))
                               || (state != null && state.StateName.ToLower().Contains(searchText.ToLower()))
                           )

                     orderby showroom.ShowroomId descending

                     select new ShowroomDTO
                     {
                         ShowroomId = showroom.ShowroomId,
                         ShowroomName = showroom.ShowroomName,
                         GoogleLink = showroom.GoogleLink,
                         DealerName = showroom.DealerName,
                         PrimaryContact = showroom.PrimaryContact,
                         SecondaryContact = showroom.SecondaryContact,
                         Address = showroom.Address,
                         CountryId = showroom.CountryId,
                         StateId = showroom.StateId,
                         CityId = showroom.CityId,
                         State = state.StateName,
                         City = city.CityName,
                         Recentvisit = StringUtility.ToRelativeTimeString(showroom.CreatedAt),
                     }).ToList();

                if (showroomObj.Any())
                {
                    var totalRecords = showroomObj.Count;

                    showroomObj = showroomObj
                        .Skip((pageIndex - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    showroomObj.First().TotalRecords = totalRecords;
                }

                return showroomObj;
            }
            catch
            {
                return null;
            }
        }

        public ShowroomDTO AddShowroom(ShowroomDTO showroomDTO, Guid aspnetUserId)
		{
			ShoShowroom showroom = new ShoShowroom();
			showroom.ShowroomName = showroomDTO.ShowroomName;
			showroom.GoogleLink = showroomDTO.GoogleLink;
			showroom.DealerName = showroomDTO.DealerName;
			//showroom.ContactPersonName = showroomDTO.ContactPersonName;
			showroom.PrimaryContact = showroomDTO.PrimaryContact;
			showroom.SecondaryContact = showroomDTO.SecondaryContact;
			showroom.Address = showroomDTO.Address;
			showroom.CountryId = 101;
			showroom.StateId = showroomDTO.StateId;
			showroom.CityId = showroomDTO.CityId;
			showroom.IsDeleted = false;
            //lat/long save
            //var (lat, lng) = GoogleMapHelper.ExtractCoordinates(showroomDTO.GoogleLink);
            //showroom.Latitude = lat;
            //showroom.Longitude = lng;
            var point = GoogleMapLinkParser.Instance.Parse(showroomDTO.GoogleLink);
            showroom.Latitude = point?.Latitude;
            showroom.Longitude = point?.Longitude;

            showroom.CreatedAt = DateTime.Now;
			showroom.CreatedBy = aspnetUserId;
			showroom.ModifiedAt = DateTime.Now;
			showroom.ModifiedBy = aspnetUserId;

			_context.ShoShowrooms.Add(showroom);
			_context.SaveChanges();

			showroomDTO.ShowroomId = showroom.ShowroomId;
			return showroomDTO;
		}

		public ShowroomDTO EditShowroom(ShowroomDTO showroomDTO, Guid aspnetUserId)
		{
			var showroom = _context.ShoShowrooms.FirstOrDefault(x => x.ShowroomId == showroomDTO.ShowroomId && x.IsDeleted == false);

			if (showroom == null)
			{
				return null;
			}

            showroom.ShowroomName = showroomDTO.ShowroomName;
            showroom.GoogleLink = showroomDTO.GoogleLink;
            showroom.DealerName = showroomDTO.DealerName;
            //showroom.ContactPersonName = showroomDTO.ContactPersonName;
            showroom.PrimaryContact = showroomDTO.PrimaryContact;
            showroom.SecondaryContact = showroomDTO.SecondaryContact;
            showroom.Address = showroomDTO.Address;
            showroom.StateId = showroomDTO.StateId;
            showroom.CityId = showroomDTO.CityId;
            showroom.ModifiedAt = DateTime.Now;

            //lat/long save
            //var (lat, lng) = GoogleMapHelper.ExtractCoordinates(showroomDTO.GoogleLink);
            //showroom.Latitude = lat;
            //showroom.Longitude = lng;
            var point = GoogleMapLinkParser.Instance.Parse(showroomDTO.GoogleLink);
            showroom.Latitude = point?.Latitude;
            showroom.Longitude = point?.Longitude;

            showroom.ModifiedBy = aspnetUserId;

            _context.SaveChanges();
			return showroomDTO;
		}

		public bool DeleteShowroom(int showroomId, Guid aspnetUserId)
		{
			var showroom = _context.ShoShowrooms.FirstOrDefault(x => x.ShowroomId == showroomId && x.IsDeleted == false);

			if (showroom == null)
			{
				return false;
			}

			showroom.IsDeleted = true;
			showroom.DeletedAt = DateTime.Now;
			showroom.DeletedBy = aspnetUserId;
			showroom.ModifiedAt = DateTime.Now;
			showroom.ModifiedBy = aspnetUserId;

			_context.SaveChanges();
			return true;
		}

		public ShowroomDetailDTO GetShowroomById(int showroomId)
		{
			var showroom = (from s in _context.ShoShowrooms
							join state in _context.LocStates on s.StateId equals state.StateId into stateJoin
							from state in stateJoin.DefaultIfEmpty()
							join city in _context.LocCities on s.CityId equals city.CityId into cityJoin
							from city in cityJoin.DefaultIfEmpty()
							where s.ShowroomId == showroomId && s.IsDeleted == false
							select new ShowroomDetailDTO
							{
								ShowroomId = s.ShowroomId,
								ShowroomName = s.ShowroomName,
								GoogleLink = s.GoogleLink,
								DealerName = s.DealerName,
								PrimaryContact = s.PrimaryContact,
								SecondaryContact = s.SecondaryContact,
								Address = s.Address,
								City = city.CityName,
								State = state.StateName,
                                Latitude = s.Latitude,
                                Longitude = s.Longitude,

                            }).FirstOrDefault();

			if (showroom == null)
			{
				return null;
			}

            //var lastVisitDate = _context.VisVisits
            //	.Where(v => v.ShowroomId == showroomId && v.IsDeleted == false)
            //	.OrderByDescending(v => v.VisitDate)
            //	.Select(v => v.VisitDate)
            //	.FirstOrDefault();

            //if (lastVisitDate == default)
            //{
            //	showroom.LastVisit = "No visits yet";
            //}
            //else
            //{
            //	showroom.LastVisit = StringUtility.ToRelativeTimeString(lastVisitDate);
            //             showroom.Latitude = showroom.Latitude;
            //             showroom.Longitude = showroom.Longitude;
            //}

            var lastVisit = _context.VisVisits
				.Where(v => v.ShowroomId == showroomId && v.IsDeleted == false)
				.OrderByDescending(v => v.VisitDate)
				.FirstOrDefault();

            if (lastVisit == null)
            {
                showroom.LastVisit = "No visits yet";
            }
            else
            {
                showroom.LastVisit = StringUtility.ToRelativeTimeString(lastVisit.VisitDate);
                //showroom.Latitude = lastVisit.Latitude;
                //showroom.Longitude = lastVisit.Longitude;
                showroom.Latitude = showroom.Latitude ?? lastVisit.Latitude;
                showroom.Longitude = showroom.Longitude ?? lastVisit.Longitude;
            }

            showroom.VisitTimeline = (from v in _context.VisVisits
									  join sp in _context.SecSalesPeople on v.SalesPersonId equals sp.SalesPersonId
									  where v.ShowroomId == showroomId && v.IsDeleted == false
									  orderby v.VisitDate descending
									  select new ShowroomVisitTimelineDTO
									  {
										  VisitId = v.VisitId,
										  SalesPersonName = sp.SalesPersonName,
										  VisitDate = v.VisitDate,
										  DiscussionNotes = v.DiscussionNotes,
										  //Products = v.ProductsDiscussedString.Split("@#$%^&**&^%$#@", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
									  }).ToList();

            //--------------------------------------------------------

            showroom.Photos = new List<string>();

            var visits = _context.VisVisits
                .Where(v => v.ShowroomId == showroomId && v.IsDeleted == false)
                .OrderByDescending(v => v.VisitDate)
                .ToList();

            foreach (var visit in visits)
            {
                if (!string.IsNullOrEmpty(visit.VisitingCardFrontPath))
                {
                    showroom.Photos.Add(CommonHelper.GetFullImagePath(visit.VisitingCardFrontPath));
                }

                if (!string.IsNullOrEmpty(visit.VisitingCardBackPath))
                {
                    showroom.Photos.Add(CommonHelper.GetFullImagePath(visit.VisitingCardBackPath));
                }

                var attachments = _context.VisVisitWiseAttachments
                    .Where(a => a.VisitId == visit.VisitId && a.IsDeleted == false)
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.AttachmentPath)
                    .ToList();

                foreach (var attachmentPath in attachments)
                {
                    showroom.Photos.Add(CommonHelper.GetFullImagePath(attachmentPath));
                }
            }

            return showroom;
        }
	}
}
