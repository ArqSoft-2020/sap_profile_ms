using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
//using System.DirectoryServices.Protocols;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;

namespace sap_profile_ms.LDAP
{
    public class ServiceLDAP
    {
        private static readonly string Host = "ec2-3-210-210-169.compute-1.amazonaws.com";

        private static readonly int Port = 389;
        private static readonly string dn = "cn=admin,dc=hangeddraw,dc=arqsoft,dc=unal,dc=edu,dc=co";
        private static readonly string pa = "admin";

        // cn=user,dc=hangeddraw,dc=arqsoft,dc=unal,dc=edu,dc=co
        // ou=hangeddraw,dc=hangeddraw,dc=arqsoft,dc=unal,dc=edu,dc=co
        // ou=pprueba,dc=hangeddraw,dc=arqsoft,dc=unal,dc=edu,dc=co password Proyecto.123

        private static readonly string filter = "ou=hangeddraw,dc=hangeddraw,dc=arqsoft,dc=unal,dc=edu,dc=co";


        public static Task<bool> LoginAsync(string username, string password)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;

            LdapConnection conn = null;


            return Task.Factory.StartNew(() => {

                conn = new LdapConnection();
                conn.Connect(Host, Port);

                if (!string.IsNullOrEmpty(username))
                {

                    try
                    {
                        conn.Bind(dn, pa);
                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    string searchBase = filter;

                    int searchScope = LdapConnection.SCOPE_SUB;
                    string searchFilter = "uid=" + username.Trim();
                    LdapSearchQueue queue = conn.Search(searchBase,
                                                            searchScope,
                                                            searchFilter,
                                                            null,
                                                            false,
                                                            (LdapSearchQueue)null,
                                                            (LdapSearchConstraints)null);

                    LdapMessage message;
                    while ((message = queue.getResponse()) != null)
                    {
                        try
                        {
                            string msg = message.ToString();

                            LdapEntry entry = ((LdapSearchResult)message).Entry;

                            LdapAttributeSet attributeSet = entry.getAttributeSet();
                            System.Collections.IEnumerator ienum = attributeSet.GetEnumerator();

                            LdapAttribute cn = attributeSet.getAttribute("cn");
                            string idUser = cn.StringValue;

                            try
                            {
                                conn.Bind("cn=" + idUser + "," + filter, password);

                            }
                            catch (Exception e)
                            {
                                return false;
                            }

                            return true;
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }


                }

                return false;

            }, cancellationToken); 
        }

        public static Task<bool> RegisterAsync(string username, string password, string nombre, string apellido, string email )
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;

            LdapConnection conn = null;


            return Task.Factory.StartNew(() => {

                try
                {
                    try
                    {
                        conn = new LdapConnection();
                        conn.Connect(Host, Port);

                        conn.Bind( dn, pa);

                        LdapAttributeSet ldapAttributeSet = new LdapAttributeSet();
                        ldapAttributeSet.Add(new LdapAttribute("cn", nombre + " " + apellido));
                        ldapAttributeSet.Add(new LdapAttribute("sn", nombre));
                        ldapAttributeSet.Add(new LdapAttribute("homeDirectory", "/home/users/" + username));
                        ldapAttributeSet.Add(new LdapAttribute("objectClass", new string[] { "inetOrgPerson", "posixAccount", "top" }));
                        ldapAttributeSet.Add(new LdapAttribute("uid", username));
                        ldapAttributeSet.Add(new LdapAttribute("givenName", nombre));
                        ldapAttributeSet.Add(new LdapAttribute("uidNumber", "1000"));
                        ldapAttributeSet.Add(new LdapAttribute("gidNumber", "500"));
                        ldapAttributeSet.Add(new LdapAttribute("mail", email));
                        ldapAttributeSet.Add(new LdapAttribute("userPassword", password));

                        LdapEntry ldapEntry = new LdapEntry("cn=" + nombre + " " + apellido + "," + filter, ldapAttributeSet);

                        conn.Add(ldapEntry);

                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
                
            }, cancellationToken);
        }

        public static Task<bool> DeleteAsync(string username)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;

            LdapConnection conn = null;


            return Task.Factory.StartNew(() => {

                conn = new LdapConnection();
                conn.Connect(Host, Port);

                if (!string.IsNullOrEmpty(username))
                {

                    try
                    {
                        conn.Bind(dn, pa);
                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    string searchBase = filter;

                    int searchScope = LdapConnection.SCOPE_SUB;
                    string searchFilter = "uid=" + username.Trim();
                    LdapSearchQueue queue = conn.Search(searchBase,
                                                            searchScope,
                                                            searchFilter,
                                                            null,
                                                            false,
                                                            (LdapSearchQueue)null,
                                                            (LdapSearchConstraints)null);

                    LdapMessage message;
                    while ((message = queue.getResponse()) != null)
                    {
                        try
                        {
                            string msg = message.ToString();

                            LdapEntry entry = ((LdapSearchResult)message).Entry;

                            LdapAttributeSet attributeSet = entry.getAttributeSet();
                            System.Collections.IEnumerator ienum = attributeSet.GetEnumerator();

                            LdapAttribute cn = attributeSet.getAttribute("cn");
                            string idUser = cn.StringValue;

                            try
                            {
                                conn.Delete("cn=" + idUser + "," + filter);

                            }
                            catch (Exception e)
                            {
                                return false;
                            }

                            return true;

                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }


                }

                return false;

            }, cancellationToken);
        }

        public static Task<bool> ModifyAsync(string oldUsername, string username, string password, string nombre, string apellido, string email)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;

            LdapConnection conn = null;


            return Task.Factory.StartNew(() => {

                conn = new LdapConnection();
                conn.Connect(Host, Port);

                if (!string.IsNullOrEmpty(username))
                {

                    try
                    {
                        conn.Bind(dn, pa);
                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    string searchBase = filter;

                    int searchScope = LdapConnection.SCOPE_SUB;
                    string searchFilter = "uid=" + username.Trim();
                    LdapSearchQueue queue = conn.Search(searchBase,
                                                            searchScope,
                                                            searchFilter,
                                                            null,
                                                            false,
                                                            (LdapSearchQueue)null,
                                                            (LdapSearchConstraints)null);

                    LdapMessage message;
                    while ((message = queue.getResponse()) != null)
                    {
                        try
                        {
                            string msg = message.ToString();

                            LdapEntry entry = ((LdapSearchResult)message).Entry;

                            LdapAttributeSet attributeSet = entry.getAttributeSet();
                            System.Collections.IEnumerator ienum = attributeSet.GetEnumerator();

                            LdapAttribute cn = attributeSet.getAttribute("cn");
                            string idUser = cn.StringValue;

                            try
                            {
                                conn.Delete("cn=" + idUser + "," + filter);


                                LdapAttributeSet ldapAttributeSet = new LdapAttributeSet();
                                ldapAttributeSet.Add(new LdapAttribute("cn", nombre + " " + apellido));
                                ldapAttributeSet.Add(new LdapAttribute("sn", nombre));
                                ldapAttributeSet.Add(new LdapAttribute("homeDirectory", "/home/users/" + username));
                                ldapAttributeSet.Add(new LdapAttribute("objectClass", new string[] { "inetOrgPerson", "posixAccount", "top" }));
                                ldapAttributeSet.Add(new LdapAttribute("uid", username));
                                ldapAttributeSet.Add(new LdapAttribute("givenName", nombre));
                                ldapAttributeSet.Add(new LdapAttribute("uidNumber", "1000"));
                                ldapAttributeSet.Add(new LdapAttribute("gidNumber", "500"));
                                ldapAttributeSet.Add(new LdapAttribute("mail", email));
                                ldapAttributeSet.Add(new LdapAttribute("userPassword", password));

                                LdapEntry ldapEntry = new LdapEntry("cn=" + nombre + " " + apellido + "," + filter, ldapAttributeSet);

                                conn.Add(ldapEntry);

                            }
                            catch (Exception e)
                            {
                                return false;
                            }

                            return true;

                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }


                }

                return false;

            }, cancellationToken);
        }


    }

}

