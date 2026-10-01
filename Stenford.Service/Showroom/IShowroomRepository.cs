using Stenford.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Stenford.Domain.DTO;

namespace Stenford.Service.Showroom
{
	public interface IShowroomRepository
	{
		List<ShowroomDTO> GetShowroomDataList(string searchText, int pageIndex, int pageSize, int? stateId, int? cityId, Guid aspNetUserId);

		ShowroomDTO AddShowroom(ShowroomDTO showroomDTO, Guid aspnetUserId);

		ShowroomDTO EditShowroom(ShowroomDTO showroomDTO, Guid aspnetUserId);

		bool DeleteShowroom(int showroomId, Guid aspnetUserId);

		ShowroomDetailDTO GetShowroomById(int showroomId);

	}
}
