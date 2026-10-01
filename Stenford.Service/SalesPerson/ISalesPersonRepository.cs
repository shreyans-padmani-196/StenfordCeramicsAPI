
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Stenford.Domain.DTO;

namespace Stenford.Service.SalesPerson
{
	public interface ISalesPersonRepository
	{
		List<SalesPersonDTO> GetSalesPersonDataList(int pageNumber, int pageSize, string? search, int? stateId, int? cityId);

		SalesPersonDTO AddSalesPerson(SalesPersonDTO salesPersonDTO, string aspnetUserId);

		SalesPersonDTO EditSalesPerson(SalesPersonDTO salesPersonDTO, string aspnetUserId);

        //bool IsSalesPersonNameExists(string salesPersonName);
        //bool IsSalesPersonNameExists(string salesPersonName, int? excludeSalesPersonId = null);

        bool DeleteSalesPerson(int salesPersonId, string aspnetUserId);

		SalesPersonDetailDTO GetSalesPersonById(int salesPersonId);
    }
}
