using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TentacionSana.Application.Security;
using TentacionSana.Infrastructure.Auditing;
using TentacionSana.Infrastructure.Persistence;

namespace TentacionSana.Infrastructure.Identity;

public sealed class UserAdministrationService(ApplicationDbContext db,UserManager<ApplicationUser> users,RoleManager<IdentityRole<Guid>> roles,TimeProvider clock) : IUserAdministrationService
{
    public async Task<UserAdministrationSnapshot> GetAsync(CancellationToken cancellationToken=default)
    {
        var ct=cancellationToken;var roleRows=await roles.Roles.OrderBy(x=>x.Name).ToListAsync(ct);
        var managedRoles=new List<ManagedRole>();
        foreach(var role in roleRows)
        {
            var claims=await roles.GetClaimsAsync(role);
            managedRoles.Add(new(role.Id,role.Name??"—",AppRoles.All.Contains(role.Name??""),claims.Where(x=>x.Type==AppPermissions.ClaimType).Select(x=>x.Value).ToList(),await db.UserRoles.CountAsync(x=>x.RoleId==role.Id,ct)));
        }
        var userRows=await users.Users.OrderBy(x=>x.DisplayName).ToListAsync(ct);
        var managedUsers=new List<ManagedUser>();
        foreach(var user in userRows)
        {
            var names=await users.GetRolesAsync(user);var ids=roleRows.Where(x=>names.Contains(x.Name!)).Select(x=>x.Id).ToList();
            managedUsers.Add(new(user.Id,user.DisplayName,user.UserName??"",user.IsEnabled,user.MustChangePassword,ids,names.ToList()));
        }
        return new(managedUsers,managedRoles,AppPermissions.All);
    }

    public async Task<SecurityOperationResult> CreateUserAsync(CreateUserCommand command,Guid actorId,CancellationToken cancellationToken=default)
    {
        var ct=cancellationToken;
        if(!await IsAdministrator(actorId))return Fail("Solo una persona administradora puede crear usuarios.");
        if(string.IsNullOrWhiteSpace(command.DisplayName)||string.IsNullOrWhiteSpace(command.UserName))return Fail("El nombre y el usuario son obligatorios.");
        var selected=await roles.Roles.Where(x=>command.RoleIds.Contains(x.Id)).ToListAsync(ct);if(selected.Count==0)return Fail("Selecciona al menos un rol.");
        var user=new ApplicationUser{Id=Guid.NewGuid(),DisplayName=command.DisplayName.Trim(),UserName=command.UserName.Trim(),IsEnabled=true,MustChangePassword=true,CreatedAtUtc=clock.GetUtcNow()};
        var created=await users.CreateAsync(user,command.TemporaryPassword);if(!created.Succeeded)return Errors(created.Errors);
        var assigned=await users.AddToRolesAsync(user,selected.Select(x=>x.Name!));if(!assigned.Succeeded){await users.DeleteAsync(user);return Errors(assigned.Errors);}
        Audit("UserCreated",user.Id,actorId,new{user.DisplayName,user.UserName,Roles=selected.Select(x=>x.Name)},null);await db.SaveChangesAsync(ct);return Ok();
    }

    public async Task<SecurityOperationResult> UpdateUserAsync(UpdateUserCommand command,Guid actorId,CancellationToken cancellationToken=default)
    {
        var ct=cancellationToken;
        if(!await IsAdministrator(actorId))return Fail("Solo una persona administradora puede modificar usuarios.");
        var user=await users.FindByIdAsync(command.UserId.ToString());if(user is null)return Fail("El usuario no existe.");
        var selected=await roles.Roles.Where(x=>command.RoleIds.Contains(x.Id)).ToListAsync(ct);if(selected.Count==0)return Fail("Selecciona al menos un rol.");
        if(user.Id==actorId&&(!command.IsEnabled||selected.All(x=>x.Name!=AppRoles.Administrator)))return Fail("No puedes deshabilitar tu propia cuenta ni quitarte el rol Administrador.");
        var previous=new{user.DisplayName,user.IsEnabled,Roles=await users.GetRolesAsync(user)};
        user.DisplayName=command.DisplayName.Trim();user.IsEnabled=command.IsEnabled;user.DisabledAtUtc=command.IsEnabled?null:clock.GetUtcNow();
        var update=await users.UpdateAsync(user);if(!update.Succeeded)return Errors(update.Errors);
        var current=await users.GetRolesAsync(user);var remove=await users.RemoveFromRolesAsync(user,current.Except(selected.Select(x=>x.Name!)));if(!remove.Succeeded)return Errors(remove.Errors);
        var add=await users.AddToRolesAsync(user,selected.Select(x=>x.Name!).Except(current));if(!add.Succeeded)return Errors(add.Errors);
        await users.UpdateSecurityStampAsync(user);Audit("UserUpdated",user.Id,actorId,new{user.DisplayName,user.IsEnabled,Roles=selected.Select(x=>x.Name)},previous);await db.SaveChangesAsync(ct);return Ok();
    }

    public async Task<SecurityOperationResult> CreateRoleAsync(CreateRoleCommand command,Guid actorId,CancellationToken cancellationToken=default)
    {
        var ct=cancellationToken;
        if(!await IsAdministrator(actorId))return Fail("Solo una persona administradora puede crear roles.");
        var name=command.Name.Trim();if(string.IsNullOrWhiteSpace(name))return Fail("El nombre del rol es obligatorio.");if(await roles.RoleExistsAsync(name))return Fail("Ya existe un rol con ese nombre.");
        var role=new IdentityRole<Guid>(name){Id=Guid.NewGuid()};var created=await roles.CreateAsync(role);if(!created.Succeeded)return Errors(created.Errors);
        var permissionResult=await ReplacePermissions(role,command.Permissions);if(!permissionResult.Succeeded){await roles.DeleteAsync(role);return permissionResult;}
        Audit("RoleCreated",role.Id,actorId,new{Name=name,Permissions=command.Permissions},null);await db.SaveChangesAsync(ct);return Ok();
    }

    public async Task<SecurityOperationResult> UpdateRolePermissionsAsync(UpdateRolePermissionsCommand command,Guid actorId,CancellationToken cancellationToken=default)
    {
        var ct=cancellationToken;
        if(!await IsAdministrator(actorId))return Fail("Solo una persona administradora puede modificar roles.");
        var role=await roles.FindByIdAsync(command.RoleId.ToString());if(role is null)return Fail("El rol no existe.");
        if(role.Name==AppRoles.Administrator)return Fail("El rol Administrador conserva acceso total y no se puede limitar.");
        var previous=(await roles.GetClaimsAsync(role)).Where(x=>x.Type==AppPermissions.ClaimType).Select(x=>x.Value).ToList();
        var result=await ReplacePermissions(role,command.Permissions);if(!result.Succeeded)return result;
        Audit("RolePermissionsUpdated",role.Id,actorId,new{role.Name,Permissions=command.Permissions},new{role.Name,Permissions=previous});await db.SaveChangesAsync(ct);return Ok();
    }

    private async Task<SecurityOperationResult> ReplacePermissions(IdentityRole<Guid> role,IReadOnlyList<string> requested)
    {
        var valid=requested.Intersect(AppPermissions.All.Select(x=>x.Key)).Distinct().ToList();var current=(await roles.GetClaimsAsync(role)).Where(x=>x.Type==AppPermissions.ClaimType).ToList();
        foreach(var claim in current){var result=await roles.RemoveClaimAsync(role,claim);if(!result.Succeeded)return Errors(result.Errors);}
        foreach(var permission in valid){var result=await roles.AddClaimAsync(role,new Claim(AppPermissions.ClaimType,permission));if(!result.Succeeded)return Errors(result.Errors);}
        return Ok();
    }
    private async Task<bool> IsAdministrator(Guid id){var user=await users.FindByIdAsync(id.ToString());if(user is null)return false;if(await users.IsInRoleAsync(user,AppRoles.Administrator))return true;var names=await users.GetRolesAsync(user);foreach(var name in names){var role=await roles.FindByNameAsync(name);if(role is not null&&(await roles.GetClaimsAsync(role)).Any(x=>x.Type==AppPermissions.ClaimType&&x.Value==AppPermissions.Administration))return true;}return false;}
    private void Audit(string action,Guid entity,Guid actor,object values,object? previous)=>db.AuditEntries.Add(new AuditEntry{Id=Guid.NewGuid(),UserId=actor,Action=action,EntityType="Security",EntityId=entity.ToString(),PreviousValuesJson=previous is null?null:JsonSerializer.Serialize(previous),NewValuesJson=JsonSerializer.Serialize(values),OccurredAtUtc=clock.GetUtcNow()});
    private static SecurityOperationResult Ok()=>new(true,[]);private static SecurityOperationResult Fail(string error)=>new(false,[error]);private static SecurityOperationResult Errors(IEnumerable<IdentityError> errors)=>new(false,errors.Select(x=>x.Description).ToList());
}
