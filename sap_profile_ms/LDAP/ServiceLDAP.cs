using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace sap_profile_ms.LDAP
{
    public class ServiceLDAP
    {
        private static readonly string Host = "ec2-3-210-210-169.compute-1.amazonaws.com";
        //private static string Host = "52.88.217.142";

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

                //Person person = null;

                conn = new LdapConnection();
                conn.Connect(Host, Port);

                if (!string.IsNullOrEmpty(username))
                {

                    try
                    {
                        //conn.Bind("uid =" + username.Trim() + ", " + filter, password, new LdapConstraints());
                        conn.Bind(dn, pa);
                       // conn.Bind(f, pa, new LdapConstraints());
                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    //return false;

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
                                return true;

                            }
                            catch (Exception e)
                            {
                                //conn.Disconnect();
                                return false;
                            }

                            Console.WriteLine(true);

                            //LdapAttribute ac = attributeSet.getAttribute("inetUserStatus");
                            //string acti = ac.StringValue;
                            //bool active = acti.Equals("ACTIVE");

                            //LdapAttribute email = attributeSet.getAttribute("mail");
                            //string emailUser = email.StringValue;

                            //LdapAttribute name = attributeSet.getAttribute("cn");
                            //string nameUser = name.StringValue;

                            //if (active)
                            //{
                            

                            //}
                            //else
                            //{
                            //    conn.Disconnect();
                            //    return false;
                            //}
                        }
                        catch (Exception e)
                        {
                            //conn.Disconnect();
                            return false;
                        }
                    }


                }

                //conn.Disconnect();
                return false;

            }, cancellationToken); 
        }
    }

}

