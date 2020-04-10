using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using sap_profile_ms.Model;
using sap_profile_ms.Model.Identity;
using sap_profile_ms.Model.ViewModels;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace sap_profile_ms.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]/[action]")]
    public class UserController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private Context _dbContext;
        private readonly IConfiguration _configuration;

        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.USEast2;
        public static string _bucketName = "hangeddrawbucket";
        private static readonly BasicAWSCredentials awsCreds = new BasicAWSCredentials("", "");
        private static readonly string URI_S3 = "https://hangeddrawbucket.s3.us-east-2.amazonaws.com/";


        public UserController(Context dbContext,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _signInManager = signInManager;
            _userManager = userManager;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult> Register([FromBody]ViewModelUser model)
        {
            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Name = model.Name,
                    LastName = model.LastName,
                    Email = model.Email,
                    Country = model.Country,
                    Picture = model.Picture,
                    Verified = false,
                    WonGames = 0,
                    LostGames = 0,
                    TotalGames = 0
                };
                if (!model.Password.Equals(model.ConfirmedPassword))
                {
                    return Json(new ViewModelResponseRegister() { Error = true, Result = "Las contraseñas no coinciden" });
                }

                var result = _userManager.CreateAsync(user, model.Password);
                if (result.Result.Succeeded)
                {
                    // enviar correo para verificar usuario registrado
                    string email = model.Email;
                    string subject = "Confirmación de registro en Hanged Draw";
                    string url = Request.Scheme + "://" + Request.Host.Value + "/api/User/Verify";
                    string link = String.Format("<a target=\"_blank\" href=\"{1}/{0}\"> link </a>", user.Id, url);
                    string style = "style=\"color: red;\"";
                    string styleP = "style=\"color: black;\"";

                    string htmlString =
                                    $@"<html> 
                            <body> 
                                <h2 {style}>Hanged Draw</h2>                      
                                <p {styleP} >por favor verifique su cuenta dando click en el siguiente {link} </p>
                                <br>
                            </body> 
                        </html>";


                    bool a = await SendEmailAsync(email, subject, htmlString);
                    if (a)
                        return Json(new ViewModelResponseRegister() { Error = false, Result = "Usuario registrado satisfactoriamente." });

                }

                string error = string.Empty;
                foreach (var e in result.Result.Errors)
                {
                    error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                }

                return Json(new ViewModelResponseRegister() { Error = true, Result = error });
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e);
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult> Verify(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user != null)
                {
                    user.Verified = true;
                    _dbContext.SaveChanges();
                    return Json(new { Error = false, Response = $@"Hola, {user.Name} tu correo electrónico fue verificado satisfactoriamente, ahora puedes iniciar sesión." });
                }
            }
            catch (Exception e)
            {
                return Json(new { Error = "true", Response = "Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente.", Ex = e.Message });

            }

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult> UserInfo(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user != null)
                {
                    var httpClient = new WebClient();
                    byte[] bytes;
                    try
                    {
                        bytes = await httpClient.DownloadDataTaskAsync(user.Picture);
                    }
                    catch (TaskCanceledException)
                    {
                        System.Console.WriteLine("Task Canceled!");
                        bytes = null;
                    }
                    catch (Exception e)
                    {
                        bytes = null;
                    }

                    ViewModelUser model = new ViewModelUser()
                    {
                        Id = new Guid(user.Id),
                        Name = user.Name,
                        LastName = user.LastName,
                        UserName = user.UserName,
                        Email = user.Email,
                        Country = user.Country,
                        ImageBytes = bytes
                     };

                    return Json(new { Error = false, Response="Datos obtenidos satisfactoriamente.", User = model });
                }
            }
            catch (Exception e)
            {
                return Json(new { Error = "true", Response = "Ocurrio un error al obtener la informacion del usuario, intenta nueva mente.", Ex = e.Message });

            }

            return Json(new { Error = true, Response="Usuario no encontrado." });
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<object> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                    _dbContext.SaveChanges();
                    return Json(new { Error= false, Response = "Cuenta Eliminada Satisfactoriamente.", User = user });
                }
            }
            catch (Exception e)
            {
                return Json(new { Error = "true", Response = "Ocurrio un error al eliminar el usuario, intenta nueva mente.", Ex = e.Message });

            }

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> EditUser([FromBody]ViewModelUser model, string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null)
                {
                    if (!String.IsNullOrEmpty(model.UserName))
                        user.UserName = model.UserName;
                    if (!String.IsNullOrEmpty(model.Name))
                        user.Name = model.Name;
                    if (!String.IsNullOrEmpty(model.LastName))
                        user.LastName = model.LastName;
                    if (!String.IsNullOrEmpty(model.Email))
                        user.Email = model.Email;
                    if (!String.IsNullOrEmpty(model.Country))
                        user.Country = model.Country;
                    if (!String.IsNullOrEmpty(model.Picture))
                        user.Picture = model.Picture;

                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                        return Json(new { Error = false, Response = "Datos de usuario modificados exitosamente." });
                    else
                    {
                        string error = string.Empty;
                        foreach (var e in result.Errors)
                        {
                            error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                        }
                        return Json(new { Error = true, Response = error });
                    }
                }
                return Json(new { Error = true, Response = "El usuario no existe" });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e);
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> ChangePasswordUser([FromBody]ViewModelPassword model, string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null)
                {
                    if (!model.Password.Equals(model.ConfirmedPassword))
                    {
                        return Json(new { Error = true, Response = "Las contraseñas no coinciden." });
                    }

                    var hashedNewPassword = _userManager.PasswordHasher.HashPassword(user, model.Password);
                    user.PasswordHash = hashedNewPassword;

                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                        return Json(new { Error = false, Response = "Contraseña modificada exitosamente." });
                    else
                    {
                        string error = string.Empty;
                        foreach (var e in result.Errors)
                        {
                            error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                        }
                        return Json(new { Error = true, Response = error });
                    }
                }
                return Json(new { Error = true, Response = "El usuario no existe" });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e);
            }
        }

        [AllowAnonymous]
        [HttpPost("{id}")]
        public async Task<ActionResult> RequestPasswordChange(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null)
                {
                    PasswordReminder password = new PasswordReminder()
                    {
                        IdUser = new Guid(user.Id),
                        Token = Guid.NewGuid(),
                        ExpiresAt = DateTime.Now.AddHours(1)
                    };

                    _dbContext.PasswordReminder.Add(password);
                    _dbContext.SaveChanges();

                    string email = user.Email;
                    string subject = "Solicitud cambio de contraseña en Hanged Draw";
                    string url = Request.Scheme + "://" + Request.Host.Value + "/api/User/ChangePassword";
                    string link = String.Format("<a target=\"_blank\" href=\"{1}/{0}/{2}\"> link </a>", password.Id, url, password.Token);

                    string style = "style=\"color: red;\"";
                    string styleP = "style=\"color: black;\"";

                    string htmlString =
                                    $@"<html> 
                            <body> 
                                <h2 {style}>Hanged Draw</h2>                      
                                <p {styleP} >Para cambiar su contraseña ingrese en el siguiente {link} </p>
                                <br>
                            </body> 
                        </html>";

                    bool a = await SendEmailAsync(email, subject, htmlString);

                    return Json(new { error = false, Response = "Verifique su correo electrónico." });
                }

                return Json(new { error = true, Response = "Usuario no encontrado" });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e);
            }
        }

        [AllowAnonymous]
        [HttpPut("{id}/{token}")]
        public async Task<ActionResult> ChangePassword([FromBody]ViewModelPassword model, int id, string token)
        {
            try
            {
                // verificar token de solicitud
                var pr = _dbContext.PasswordReminder.FirstOrDefault(x => x.Id == id && x.Token.Equals(new Guid(token)));
                if (pr != null)
                {
                    if (DateTime.Now.CompareTo(pr.ExpiresAt) < 0)
                    {
                        if (!model.Password.Equals(model.ConfirmedPassword))
                        {
                            return Json(new { Error = true, Response = "Las contraseñas no coinciden." });
                        }

                        var user = await _userManager.FindByIdAsync(pr.IdUser.ToString());
                        if (user != null)
                        {
                            var hashedNewPassword = _userManager.PasswordHasher.HashPassword(user, model.Password);
                            user.PasswordHash = hashedNewPassword;
                            var result = await _userManager.UpdateAsync(user);

                            if (result.Succeeded)
                                return Json(new { Error = false, Response = "Contraseña modificada exitosamente" });
                            else
                            {
                                string error = string.Empty;
                                foreach (var e in result.Errors)
                                {
                                    error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                                }
                                return Json(new { Error = true, Response = error });
                            }
                        }
                        return Json(new { Error = true, Response = "Usuario no encontrado" });

                    }
                    return Json(new { Error = true, Response = "Token de cambio de contraseña ya expiró, solicite uno nuevo." });
                }
                return Json(new { Error = true, Response = "Token de cambio de contraseña no encontrado o éste ya espiró." });
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, e);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] ViewModelLogin model)
        {
            try
            {
                var us = await _userManager.FindByNameAsync(model.UserName);
                if(us != null)
                {
                    if(us.Verified)
                    {
                        var result = await _signInManager.PasswordSignInAsync(model.UserName, model.Password, false, false);
                        if (result.Succeeded)
                        {
                            var appUser = _userManager.Users.SingleOrDefault(u => u.UserName == model.UserName);
                            var token = GenerateJwtToken(model.UserName, appUser);

                            var httpClient = new WebClient();
                            byte[] bytes;
                            try
                            {
                                bytes = await httpClient.DownloadDataTaskAsync(appUser.Picture);
                            }
                            catch (TaskCanceledException)
                            {
                                System.Console.WriteLine("Task Canceled!");
                                bytes = null;
                            }
                            catch (Exception e)
                            {
                                bytes = null;
                            }

                            ViewModelUser user = new ViewModelUser()
                            {
                                Id = new Guid(appUser.Id),
                                Name = appUser.Name,
                                LastName = appUser.LastName,
                                UserName = appUser.UserName,
                                Email = appUser.Email,
                                Country = appUser.Country,
                                ImageBytes = bytes
                            };

                            return Json(new ViewModelProfile { Error = false, Response = "Ha iniciado sesión satisfactoriamente", User = user, Token = token });
                        }
                        else
                        {
                            return Json(new ViewModelProfile { Error = true, Response = "Valide sus credenciales.", User = null, Token = null });
                        }
                    }
                    return Json(new ViewModelProfile { Error = true, Response = "Debe verificar primero su cuenta, revise su correo.", User = null, Token = null });

                }
                return Json(new ViewModelProfile { Error = true, Response = "Valide sus credenciales. Usuario no encontrado", User = null, Token = null });

                
            }
            catch(Exception e)
            {
                string error = String.Format("Ocurrion un error. Intente nuevamente. {0}", e.Message);
                return Json(new ViewModelProfile { Error = true, Response = error, User = null, Token = null });

            }
        }

        [HttpPost]
        public async Task<ActionResult> UploadFile([FromBody] ViewModelUploadFile model)
        {
            try
            {
                TransferUtility fileTransferUtility = new
                    TransferUtility(new AmazonS3Client(awsCreds, bucketRegion));


                using(var stream = new MemoryStream(model.File))
                {
                    string key = Guid.NewGuid().ToString() + model.FileName;
                    await fileTransferUtility.UploadAsync(stream,
                                               _bucketName, key);

                    //var fileTransferUtilityRequest = new TransferUtilityUploadRequest
                    //{
                    //    BucketName = _bucketName,
                    //    FilePath = URI_S3 + key,
                    //    StorageClass = S3StorageClass.StandardInfrequentAccess,
                    //    Key = key,
                    //    CannedACL = S3CannedACL.PublicRead
                    //};

                    //await fileTransferUtility.UploadAsync(fileTransferUtilityRequest);

                    return Json(new { Error = false, Url = URI_S3 + key});
                }

                

            }
            catch (AmazonS3Exception e)
            {
                string error = string.Format("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
                return Json(new { Error = true, Url = "", Response=error });

            }
            catch (Exception s3Exception)
            {
                string error = string.Format("Unknown encountered on server. Message:'{0}' when writing an object", s3Exception.Message);
                return Json(new { Error = true, Url = "", Response = error });
            }
        }

        [NonAction]
        public Task<bool> SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(Constants.MAIL_FROM, "Hanged Draw");
                mail.To.Add(email);
                mail.Subject = subject;
                mail.Body = message;
                mail.IsBodyHtml = true;

                // Send the e-mail message via the specified SMTP server.
                SmtpClient smtp = new SmtpClient(Constants.MAIL_SMTP, Constants.MAIL_SMTP_PORT);
                //smtp.UseDefaultCredentials = false;
                smtp.Credentials = new System.Net.NetworkCredential(Constants.MAIL_FROM, Constants.PASSWORD_MAIL);
                //smtp.Port = Constants.MAIL_SMTP_PORT; // You can use Port 25 if 587 is blocked (mine is!)
                //smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.EnableSsl = true;
                smtp.Send(mail);

                return Task.Run(() => true);

            }
            catch
            {

            }
            return Task.Run(() => false);

        }

        [NonAction]
        private object GenerateJwtToken(string email, IdentityUser appUser)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, appUser.Id)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtKey"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["JwtExpireDays"]));

            var token = new JwtSecurityToken(
                _configuration["JwtIssuer"],
                _configuration["JwtIssuer"],
                claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        
    }
}
