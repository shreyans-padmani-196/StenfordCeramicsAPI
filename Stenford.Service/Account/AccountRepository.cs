using Stenford.Common.Utility;
using Stenford.Domain.DataContext;
using Stenford.Domain.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Stenford.Domain.DTO;

namespace Stenford.Service.Account  
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ApplicationDbContext _context;
        public AccountRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public AspAspNetUser AreUserCredentialsValid(string userName, string encodedPassword)
        {
            var userRecord = _context.AspAspNetUsers
                .FirstOrDefault(x => x.Username == userName
                                     && x.PasswordHash == encodedPassword);

            return userRecord;
        }

        public UserJwtDTO GetAspNetUserDetail(Guid aspNetUserId)
        {
            var user = _context.AspAspNetUsers.FirstOrDefault(x => x.AspNetUserId == aspNetUserId);

            if (user == null)
            {
                return null;
            }

            var userWiseRole = _context.AspAspNetUserWiseRoles.FirstOrDefault(x => x.AspNetUserId == aspNetUserId && x.IsDeleted == false);
            int aspNetUserWiseRoleId = userWiseRole != null ? userWiseRole.AspNetUserWiseRoleId : 0;

            var admin = _context.SecAdmins.FirstOrDefault(a => a.AspNetUserId == aspNetUserId && a.IsDeleted == false);

            if (admin != null)
            {
                return new UserJwtDTO
                {
                    AspNetUserID = aspNetUserId,
                    EmailId = user.Username,
                    UserName = admin.UserName,
                    RoleId = 1,
                    RoleName = "Admin",
                    AdminID = admin.AdminId,
                    SalesPersonID = 0,
                    AspNetUserWiseRoleId = aspNetUserWiseRoleId
                };
            }

            var salesPerson = _context.SecSalesPeople.FirstOrDefault(sp => sp.AspNetUserId == aspNetUserId && sp.IsDeleted == false && sp.IsActive == true);
            if (salesPerson != null)
            {
                return new UserJwtDTO
                {
                    AspNetUserID = aspNetUserId,
                    EmailId = user.Username,
                    UserName = salesPerson.SalesPersonName,
                    RoleId = 2,
                    RoleName = "SalesPerson",
                    AdminID = 0,
                    SalesPersonID = salesPerson.SalesPersonId,
                    AspNetUserWiseRoleId = aspNetUserWiseRoleId
                };
            }

            return null;
        }

        public ProfileDTO GetProfile(Guid aspNetUserId)
        {
            try
            {
                // Step 1: Get role first
                var userWiseRole = _context.AspAspNetUserWiseRoles.FirstOrDefault(x => x.AspNetUserId == aspNetUserId && x.IsDeleted == false);
                int roleId = 0;
                string roleName = null;
                if (userWiseRole != null)
                {
                    var role = _context.AspAspNetUserRoles.FirstOrDefault(r => r.AspNetUserRoleId == userWiseRole.AspNetUserRoleId && r.IsDeleted == false);
                    if (role != null)
                    {
                        roleId = role.AspNetUserRoleId;
                        roleName = role.AspNetUserRole;
                    }
                }

                // Step 2: Check Admin first
                var admin = _context.SecAdmins.FirstOrDefault(a => a.AspNetUserId == aspNetUserId && a.IsDeleted == false);
                if (admin != null)
                {
                    return new ProfileDTO
                    {
                        AdminID = admin.AdminId,
                        UserName = admin.UserName,
                        EmailId = admin.Email,
                        RoleId = roleId,
                        RoleName = roleName
                    };
                }

                // Step 3: Otherwise check SalesPerson
                var salesPerson = (from sp in _context.SecSalesPeople
                                   join state in _context.LocStates on sp.StateId equals state.StateId into stateJoin
                                   from state in stateJoin.DefaultIfEmpty()
                                   where sp.AspNetUserId == aspNetUserId && sp.IsDeleted == false
                                   select new ProfileDTO
                                   {
                                       SalesPersonID = sp.SalesPersonId,
                                       UserName = sp.SalesPersonName,
                                       EmailId = sp.Email,
                                       PrimaryContact = sp.PrimaryContact,
                                       State = state.StateName,
                                       RoleId = roleId,
                                       RoleName = roleName
                                   }).FirstOrDefault();

                if (salesPerson == null)
                {
                    return null;
                }

                var visits = _context.VisVisits.Where(v => v.SalesPersonId == salesPerson.SalesPersonID && v.IsDeleted == false).ToList();
                salesPerson.TotalVisits = visits.Count;
                //salesPerson.ShowroomCount = visits.Select(v => v.ShowroomId).Distinct().Count();
                salesPerson.ShowroomCount = _context.ShoShowrooms.Count(s => s.CreatedBy == aspNetUserId && s.IsDeleted == false);
                salesPerson.ThisMonthVisits = visits.Count(v => v.VisitDate.Month == DateTime.Now.Month && v.VisitDate.Year == DateTime.Now.Year);

                return salesPerson;
            }
            catch
            {
                return null;
            }
        }
    }
}
