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
using sap_profile_ms.LDAP;
using sap_profile_ms.Model;
using sap_profile_ms.Model.Identity;
using sap_profile_ms.Model.ViewModels;

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
        public async Task<ActionResult<ViewModelResponse>> Register([FromBody]ViewModelUser model)
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
                    return Json(new ViewModelResponse() { Error = true, Response = "Las contraseñas no coinciden" });
                }

                //crear entry en organizacion hangeddraw, gid number user

                var result = _userManager.CreateAsync(user, model.Password);
                if (result.Result.Succeeded)
                {
                    var aLdap = await ServiceLDAP.RegisterAsync(user.UserName, model.Password, model.Name, model.LastName, model.Email);
                    if(aLdap)
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
                            return Json(new ViewModelResponse() { Error = false, Response = "Usuario registrado satisfactoriamente." });
                    }
                    else
                    {
                        return Json(new ViewModelResponse() { Error = true, Response = "Ocurrio un error en ldap" });

                    }

                }

                string error = string.Empty;
                foreach (var e in result.Result.Errors)
                {
                    error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                }

                return Json( new ViewModelResponse() { Error = true, Response = error });
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, 
                    Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<ViewModelResponse>> Verify(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user != null)
                {
                    user.Verified = true;
                    _dbContext.SaveChanges();
                    return Json( new ViewModelResponse() { Error = false, Response = $@"Hola, {user.Name} tu correo electrónico fue verificado satisfactoriamente, ahora puedes iniciar sesión." });
                }
                return Json(new ViewModelResponse() { Error = true, Response = "Usuario no encontrado" });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse(){ Error = true, 
                    Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });

            }

        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ViewModelResponse>> UserInfo(string id)
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
                        ImageBytes = bytes,
                        Picture = user.Picture
                     };

                    return Json( new ViewModelResponse(){ Error = false, Response="Datos obtenidos satisfactoriamente.", User = model });
                }
                return Json( new ViewModelResponse() { Error = true, Response = "Usuario no encontrado." });

            }
            catch (Exception e)
            {
                return StatusCode( StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al obtener la informacion del usuario, intenta nueva mente.{0}", e.Message) });

            }

        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ViewModelResponse>> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user != null)
                {
                    var ldapDelete = await ServiceLDAP.DeleteAsync(user.UserName);
                    if(ldapDelete)
                    {
                        await _userManager.DeleteAsync(user);
                        _dbContext.SaveChanges();
                        return Json(new ViewModelResponse() { Error = false, Response = "Cuenta Eliminada Satisfactoriamente." });
                    }
                    else
                        return Json(new ViewModelResponse() { Error = true, Response = "Usuario no encontrado." });

                }
                return Json( new ViewModelResponse() { Error = true, Response = "Usuario no encontrado." });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al eliminar el usuario, intenta nueva mente. {0}", e.Message) });
            }

        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ViewModelResponse>> EditUser([FromBody]ViewModelUser model, string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null)
                {
                    //var oldUN = user.UserName;

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

                    if ( model.TotalGames != 0)
                        user.TotalGames = model.TotalGames;
                    if (model.WonGames != 0)
                        user.WonGames = model.WonGames;
                    if (model.LostGames != 0)
                        user.LostGames = model.LostGames;

                    //var ldapModify = ServiceLDAP.ModifyAsync(oldUN, user.UserName, model.Password)

                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                        return Json(new ViewModelResponse(){ Error = false, Response = "Datos de usuario modificados exitosamente." });
                    else
                    {
                        string error = string.Empty;
                        foreach (var e in result.Errors)
                        {
                            error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                        }
                        return Json( new ViewModelResponse() { Error = true, Response = error });
                    }
                }
                return Json(new ViewModelResponse() { Error = true, Response = "El usuario no existe" });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });

            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ViewModelResponse>> ChangePasswordUser([FromBody]ViewModelPassword model, string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null)
                {
                    if (!model.NewPassword.Equals(model.ConfirmedNewPassword))
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new ViewModelResponse() { Error = true, Response = "Las contraseñas no coinciden." });
                    }


                    var result = await _userManager.ChangePasswordAsync(user, model.Password, model.NewPassword);

                    if (result.Succeeded)
                    {
                        var aLdap = await ServiceLDAP.ModifyAsync(user.UserName, user.UserName, model.NewPassword, user.Name, user.LastName, user.Email);
                        if(aLdap)
                        {
                            return Json(new ViewModelResponse() { Error = false, Response = "Contraseña modificada exitosamente." });
                        }
                        else
                        {
                            return Json(new ViewModelResponse() { Error = true, Response = "Ocurrio un error" });
                        }

                    }
                    else
                    {
                        string error = string.Empty;
                        foreach (var e in result.Errors)
                        {
                            error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                        }
                        return Json( new ViewModelResponse() { Error = true, Response = error });
                    }
                }
                return Json(new ViewModelResponse() { Error = true, Response = "El usuario no existe" });


            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });

            }
        }

        [AllowAnonymous]
        [HttpPost("{email}")]
        public async Task<ActionResult<ViewModelResponse>> RequestPasswordChange(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
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

                    string emailTo = user.Email;
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

                    bool a = await SendEmailAsync(emailTo, subject, htmlString);

                    return Json( new  ViewModelResponse() { Error = false, Response = "Verifique su correo electrónico." });
                }
                return Json( new ViewModelResponse() { Error = true, Response = "El usuario no existe" });
            }
            catch (Exception e)
            {
                return Json( new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });

            }
        }

        [AllowAnonymous]
        [HttpPut("{id}/{token}")]
        public async Task<ActionResult<ViewModelResponse>> ChangePassword([FromBody]ViewModelPassword model, int id, string token)
        {
            try
            {
                // verificar token de solicitud
                var pr = _dbContext.PasswordReminder.FirstOrDefault(x => x.Id == id && x.Token.Equals(new Guid(token)));
                if (pr != null)
                {
                    if (DateTime.Now.CompareTo(pr.ExpiresAt) < 0)
                    {
                        if (!model.NewPassword.Equals(model.ConfirmedNewPassword))
                        {
                            return Json(new ViewModelResponse() { Error = true, Response = "Las contraseñas no coinciden." });
                        }

                        var user = await _userManager.FindByIdAsync(pr.IdUser.ToString());
                        if (user != null)
                        {
                            var result = await _userManager.ChangePasswordAsync(user, model.Password, model.NewPassword);

                            if (result.Succeeded)
                            {
                                var aLdap = await ServiceLDAP.ModifyAsync(user.UserName, user.UserName, model.NewPassword, user.Name, user.LastName, user.Email);
                                if (aLdap)
                                {
                                    _dbContext.PasswordReminder.Remove(pr);
                                    _dbContext.SaveChanges();
                                    return Json(new ViewModelResponse() { Error = false, Response = "Contraseña modificada exitosamente." });
                                }
                                else
                                {
                                    return Json(new ViewModelResponse() { Error = true, Response = "Ocurrio un error" });
                                }
                            }
                            else
                            {
                                string error = string.Empty;
                                foreach (var e in result.Errors)
                                {
                                    error += "{" + e.Code + "}-" + e.Description + Environment.NewLine;
                                }
                                return Json( new ViewModelResponse(){ Error = true, Response = error });
                            }
                        }
                        return Json( new ViewModelResponse() { Error = true, Response = "Usuario no encontrado" });

                    }
                    return Json( new ViewModelResponse() { Error = true, Response = "Token de cambio de contraseña ya expiró, solicite uno nuevo." });
                }
                return Json(new ViewModelResponse() { Error = true, Response = "Token de cambio de contraseña no encontrado o éste ya espiró." });
            }
            catch (Exception e)
            {
                return Json( new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al intentar verificar el correo electrónico, intenta nueva mente. {0}", e.Message) });

            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<ViewModelResponse>> Login([FromBody] ViewModelLogin model)
        {
            try
            {
                var ldap = await ServiceLDAP.LoginAsync(model.UserName, model.Password);
                if(ldap)
                {
                    var us = await _userManager.FindByNameAsync(model.UserName);
                    if (us != null)
                    {
                        if (us.Verified)
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
                                    ImageBytes = bytes,
                                    Picture = appUser.Picture
                                };

                                return Json(new ViewModelResponse() { Error = false, Response = "Ha iniciado sesión satisfactoriamente", User = user, Token = token });
                            }
                            else
                            {
                                return Json(new ViewModelResponse() { Error = true, Response = "Valide sus credenciales." });

                            }
                        }
                        return Json(new ViewModelResponse() { Error = true, Response = "Debe verificar primero su cuenta, revise su correo." });
                    }
                    return Json(new ViewModelResponse() { Error = true, Response = "Valide sus credenciales. Usuario no encontrado" });
                }
                else
                {
                    return Json(new ViewModelResponse() { Error = true, Response = "Valide sus credenciales. Usuario no encontrado" });
                }
            }
            catch(Exception e)
            {
                string error = String.Format("Ocurrion un error. Intente nuevamente. {0}", e.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse { Error = true, Response = error });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ViewModelResponse>> UploadFile([FromBody] ViewModelUploadFile model)
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

                    return Json( new ViewModelResponse() { Error = false, Uri = URI_S3 + key, Response = "Imagen subida correctamente."});
                }
            }
            catch (AmazonS3Exception e)
            {
                string error = string.Format("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse(){ Error = true, Uri = "", Response=error });

            }
            catch (Exception s3Exception)
            {
                string error = string.Format("Unknown encountered on server. Message:'{0}' when writing an object", s3Exception.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new  ViewModelResponse(){ Error = true, Uri = "", Response = error });
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

        [HttpGet]
        public ActionResult<ViewModelResponse> ValidateCurrentToken(string token)
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration["JwtKey"]));

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["JwtIssuer"],
                    ValidAudience = _configuration["JwtIssuer"],
                    IssuerSigningKey = key,
                    
                }, out SecurityToken validatedToken);
            }
            catch(Exception e)
            {
                string error = string.Format("Ocurrió el siguiente error con el token: {0}.", e.Message);
                return StatusCode(StatusCodes.Status200OK, new ViewModelResponse() { Error = true, Response = error });
            }

            return Json( new ViewModelResponse() { Error = false, Response = "Token valido" });

        }

        /*
        [HttpGet]
        public async Task<IActionResult> GetLdap()
        {
            bool login = await ServiceLDAP.LoginAsync("pprueba2123", "password");
            //bool register = await ServiceLDAP.RegisterAsync("pprueba2123", "password" ,"preuba ms 2", "ms prueba234", "pruebaemail@unal.edu.co");
            bool delete = await ServiceLDAP.DeleteAsync("pprueba2");
            bool modify = await ServiceLDAP.ModifyAsync("pprueba2123", "pprueba215", "password", "preuba ms modyfi", "ms modificado", "pruebaemailmodify@unal.edu.co");

            return Ok();
        } */

        
        [HttpGet("{email}")]
        public async Task<IActionResult> ExistUser(string email)
        {
            try
            {
                var user = _dbContext.AspNetUsers.FirstOrDefault(x => x.Email.Equals(email));
               // var user = await _userManager.FindByEmailAsync(email);

                if (user != null)
                {
                    ViewModelUser model = new ViewModelUser()
                    {
                        Id = new Guid(user.Id),
                        Name = user.Name,
                        LastName = user.LastName,
                        UserName = user.UserName,
                        Email = user.Email,
                        Country = user.Country,
                        Picture = user.Picture
                    };

                    return Json(new ViewModelResponse() { Error = false, Response = "Datos obtenidos satisfactoriamente.", User = model });
                }

                return Json(new ViewModelResponse() { Error = true, Response = "Usuario no encontrado." });

            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ViewModelResponse() { Error = true, Response = String.Format("Ocurrio un error al obtener la informacion del usuario, intenta nueva mente.{0}", e.Message) });

            }
        } 


    }
}
