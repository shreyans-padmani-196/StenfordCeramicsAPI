using Microsoft.AspNetCore.Mvc;
using QCLorence.API.Helper.StringUtility;
using Stenford.Common.Constants;
using Stenford.Controllers.Admin;
using Stenford.Service.SalesPerson;
using StenfordAPI.Authmanager;
using StenfordAPI.Helper.Mapper.SalesPerson;
using StenfordAPI.Models;
using StenfordAPI.Models.Admin;
using static Stenford.Common.Constants.Enums;

namespace StenfordAPI.Controllers.Admin
{
    [AuthManager(UserType.Admin,UserType.SalesPerson)]
    [ApiController]
	[Route("admin/salespersons")]

	public class SalesPersonController : BaseController
	{
		private readonly ISalesPersonRepository _salesPersonRepository;
		public SalesPersonController(ISalesPersonRepository salesPersonRepository)
		{
			_salesPersonRepository = salesPersonRepository;
		}

		[HttpGet]

        [Route("list")]
		public BaseResponse GetSalesPersonList([FromQuery] string? searchText, [FromQuery] int? pageNumber, [FromQuery] int? pageSize, [FromQuery] int? stateId, [FromQuery] int? cityId)
		{
			try
			{
				if (!pageNumber.HasValue || !pageSize.HasValue)
				{
					return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.PageNumberAndPageSizeRequired);
				}
				if (pageNumber <= 0 || pageSize <= 0)
				{
					return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.InvalidPageNumberOrPageSize);
				}
				var salesPersonList = _salesPersonRepository.GetSalesPersonDataList(pageNumber.Value, pageSize.Value, searchText, stateId, cityId).ToModel();
                foreach (var salesPerson in salesPersonList)
                {
                    if (!string.IsNullOrEmpty(salesPerson.Password))
                    {
                        salesPerson.Password = StringUtility.DecryptString(salesPerson.Password);
                    }
                }
                return (salesPersonList.Any()) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonListFetched, salesPersonList, salesPersonList.First()?.TotalRecords) 
					: ApiSuccess(Enums.StatusCode.Ok, "SalesPerson List Empty!",new List<int>());
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

		[HttpPost]
		[Route("add")]
		public BaseResponse AddSalesPerson([FromBody] SalesPersonModel model)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
			{
                if (!string.IsNullOrEmpty(model.SecondaryContact) && model.PrimaryContact?.Trim() == model.SecondaryContact?.Trim())
                {
                    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.ContactNumbersCannotBeSame);
                }
                //if (_salesPersonRepository.IsSalesPersonNameExists(model.SalesPersonName))
                //{
                //	return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.SalesPersonNameAlreadyExists);
                //}
                var dto = model.ToModel();
				dto.Password = StringUtility.EncryptString(model.Password);

                var result = _salesPersonRepository.AddSalesPerson(dto, CV.AspNetUserId(token));
                //var result = _salesPersonRepository.AddSalesPerson(dto, "11111111-1111-1111-1111-111111111111");

                //return ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonAdded, result.ToModel());
                return (result != null) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonAdded, result.ToModel()) : ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.SalesPersonEmailAlreadyExists);
            }
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

        //[HttpPost]
        //[Route("edit")]
        //public BaseResponse EditSalesPerson([FromBody] SalesPersonModel model)
        //{
        //          string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        //          try
        //	{
        //		var dto = model.ToModel();
        //		var result = _salesPersonRepository.EditSalesPerson(dto, CV.AspNetUserId(token));
        //		//var result = _salesPersonRepository.EditSalesPerson(dto, "11111111-1111-1111-1111-111111111111");

        //              return (result != null) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonUpdated, result.ToModel()) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.SalesPersonNotFound);
        //	}
        //	catch (Exception ex)
        //	{
        //		return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
        //	}
        //}

        [HttpPost]
        [Route("edit")]
        public BaseResponse EditSalesPerson([FromBody] SalesPersonModel model)
        {
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
            {
                if (!string.IsNullOrEmpty(model.SecondaryContact) && model.PrimaryContact?.Trim() == model.SecondaryContact?.Trim())
                {
                    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.ContactNumbersCannotBeSame);
                }
                //if (_salesPersonRepository.IsSalesPersonNameExists(model.SalesPersonName, model.SalesPersonId))
                //{
                //    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.SalesPersonNameAlreadyExists);
                //}

                var dto = model.ToModel();
                if (!string.IsNullOrEmpty(model.Password))
                {
                    dto.Password = StringUtility.EncryptString(model.Password);
                }
                var result = _salesPersonRepository.EditSalesPerson(dto, CV.AspNetUserId(token));
                if (result == null)
                {
                    return ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.SalesPersonNotFound);
                }
                if (result.SalesPersonId == -1)
                {
                    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.SalesPersonEmailAlreadyExists);
                }
                return ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonUpdated, result.ToModel());
            }
            catch (Exception ex)
            {
                return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
            }
        }

        [HttpDelete]
		[Route("delete")]
		public BaseResponse DeleteSalesPerson([FromQuery] int salesPersonId)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
			{
				var result = _salesPersonRepository.DeleteSalesPerson(salesPersonId, CV.AspNetUserId(token));
				//var result = _salesPersonRepository.DeleteSalesPerson(salesPersonId, "11111111-1111-1111-1111-111111111111");
                return result ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonDeleted) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.SalesPersonNotFound);
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

        [HttpGet]
        [Route("details")]
        public BaseResponse GetSalesPersonById([FromQuery] int salesPersonId)
        {
            try
            {
                var result = _salesPersonRepository.GetSalesPersonById(salesPersonId);
                if (result != null && !string.IsNullOrEmpty(result.Password))
                {
                    result.Password = StringUtility.DecryptString(result.Password);
                }
                return (result != null) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.SalesPersonFetched, result.ToModel()) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.SalesPersonNotFound);
            }
            catch (Exception ex)
            {
                return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
            }
        }


    }
}
