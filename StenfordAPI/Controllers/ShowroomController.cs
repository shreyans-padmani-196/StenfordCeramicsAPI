using Microsoft.AspNetCore.Mvc;
using Stenford.Common.Constants;
using Stenford.Controllers.Admin;
using Stenford.Service.SalesPerson;
using Stenford.Service.Showroom;
using StenfordAPI.Authmanager;
using StenfordAPI.Helper.Mapper.Showroom;
using StenfordAPI.Models;
using static Stenford.Common.Constants.Enums;

namespace StenfordAPI.Controllers
{
	[ApiController]
    [AuthManager(UserType.Admin,UserType.SalesPerson)]

    [Route("showrooms")]
	public class ShowroomController : BaseController
	{
		private readonly IShowroomRepository _showroomRepository;

		public ShowroomController(IShowroomRepository showroomRepository)
		{
			_showroomRepository = showroomRepository;
		}

		[HttpGet]
		[Route("list")]
		public BaseResponse GetShowroomList([FromQuery] string? searchText, [FromQuery] int? pageNumber, [FromQuery] int? pageSize, [FromQuery] int? stateId, [FromQuery] int? cityId)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

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

                var aspNetUserId = Guid.Parse(CV.AspNetUserId(token));

                var showroomList = _showroomRepository.GetShowroomDataList(searchText, pageNumber.Value, pageSize.Value, stateId, cityId, aspNetUserId).ToModel();
				return (showroomList.Any()) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.ShowroomListFetched, showroomList, showroomList.First().TotalRecords) : ApiSuccess(Enums.StatusCode.Ok, "Showroom List Empty!", new List<int>());
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

		[HttpPost]
		[Route("add")]
		public BaseResponse AddShowroom([FromBody] ShowroomModel model)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
			{
                if (!string.IsNullOrEmpty(model.SecondaryContact) && model.PrimaryContact?.Trim() == model.SecondaryContact?.Trim())
                {
                    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.ContactNumbersCannotBeSame);
                }
                var dto = model.ToModel();
				var result = _showroomRepository.AddShowroom(dto, Guid.Parse(CV.AspNetUserId(token)));
				//var result = _showroomRepository.AddShowroom(dto, Guid.Parse("11111111-1111-1111-1111-111111111111"));
                return ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.ShowroomAdded, result.ToModel());
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

		[HttpPost]
		[Route("edit")]
		public BaseResponse EditShowroom([FromBody] ShowroomModel model)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
			{
                if (!string.IsNullOrEmpty(model.SecondaryContact) && model.PrimaryContact?.Trim() == model.SecondaryContact?.Trim())
                {
                    return ApiMessage(Enums.StatusCode.BadRequest, ConstantMessage.ContactNumbersCannotBeSame);
                }
                var dto = model.ToModel();
				var result = _showroomRepository.EditShowroom(dto, Guid.Parse(CV.AspNetUserId(token)));
				//var result = _showroomRepository.EditShowroom(dto, Guid.Parse("11111111-1111-1111-1111-111111111111"));
				return (result != null) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.ShowroomUpdated, result.ToModel()) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.ShowroomNotFound);
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

		[HttpDelete]
		[Route("delete")]
		public BaseResponse DeleteShowroom([FromQuery]int showroomId)
		{
            string? token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            try
			{
				var result = _showroomRepository.DeleteShowroom(showroomId, Guid.Parse(CV.AspNetUserId(token)));
				//var result = _showroomRepository.DeleteShowroom(showroomId, Guid.Parse("11111111-1111-1111-1111-111111111111"));
                return result ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.ShowroomDeleted) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.ShowroomNotFound);
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}

		[HttpGet]
		[Route("details")]
		public BaseResponse GetShowroomById([FromQuery] int showroomId)
		{
			try
			{
				var result = _showroomRepository.GetShowroomById(showroomId);
				return (result != null) ? ApiSuccess(Enums.StatusCode.Ok, ConstantMessage.ShowroomFetched, result.ToModel()) : ApiMessage(Enums.StatusCode.NotFound, ConstantMessage.ShowroomNotFound);
			}
			catch (Exception ex)
			{
				return ApiException(Enums.StatusCode.ServerError, ex.Message, ex, ConstantMessage.InternalServerError);
			}
		}
	}
}
