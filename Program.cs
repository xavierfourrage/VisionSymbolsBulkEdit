using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace VisionSymbolsBulkEdit
{
    class Program
    {

        static StreamWriter file = new StreamWriter("PIVision_Line&Text_Symbols_BulkEdit_output.txt", append: false);
        static void Main(string[] args)
        {
            file.AutoFlush = true;
            SQLdata visiondata = new SQLdata();
            Utilities util = new Utilities();

            string sqlInstance = visiondata.ValidatingSQLConnection();

            DataTable dt = new DataTable();
            dt=pullDataFromSQL(dt,sqlInstance);

            util.WriteInGreen("Connection to the PIVision SQL database successful");
            util.WriteInYellow("ALL line's weight will be overwritten to 2");
            util.WriteInYellow("ALL text's color will be overwritten to Blue ");
            util.WriteInRed("Make sure you have taken a backup of your PIVision SQL database");
            bool confirm = util.Confirm("Do you want to proceed with bulk edits?");
            if (confirm)
            {
                editDataTable(dt);
                util.WriteInBlue("Updating the SQL database... do not close the window.");
                publishToSQL(dt, sqlInstance);                   
                util.WriteInGreen("Output has been saved under: PIVision_Line&Text_Symbols_BulkEdit_output.txt");
                util.PressEnterToExit();
            }
            else { util.PressEnterToExit(); }
        }

        static public DataTable pullDataFromSQL(DataTable dataTable, string sqlserver)
        {
            
            string connString = $@"Server={sqlserver};Database=PIVision;Integrated Security=true;MultipleActiveResultSets=true"; /*---> using integrated security*/
            string query = "SELECT DisplayID,[EditorDisplay] FROM[PIVision].[dbo].[View_Displays]";

            SqlConnection conn = new SqlConnection(connString);
            SqlCommand cmd = new SqlCommand(query, conn);
            conn.Open();

            SqlDataAdapter da = new SqlDataAdapter(cmd);
         
            da.Fill(dataTable);
            conn.Close();
            da.Dispose();

            return dataTable;
        }

        static public void editDataTable(DataTable dt)
        {
            Utilities util = new Utilities();
            foreach (DataRow row in dt.Rows)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Display ID: " + row["DisplayID"].ToString());
                file.WriteLine("Display ID: " + row["DisplayID"].ToString());
                Console.ForegroundColor = ConsoleColor.White;

                JObject json = JObject.Parse(row["EditorDisplay"].ToString());

                foreach (var item in json["Symbols"])
                {
                    var config = item["Configuration"];
                    string symbolType = item["SymbolType"].ToString();
               
                    if (symbolType.Equals("line") && ((double)config["StrokeWidth"] != 2))
                    {
                            util.WriteInYellow(symbolType);                          
                            file.WriteLine(symbolType);                         

                            Console.WriteLine("StrokeWidth (old value): " + config["StrokeWidth"]);
                            file.WriteLine("StrokeWidth (old value): " + config["StrokeWidth"]);
                            
                            config["StrokeWidth"] = 2;
                            
                            util.WriteInBlue("StrokeWidth (new Value): " + config["StrokeWidth"]);                          
                            file.WriteLine("StrokeWidth (new value): " + config["StrokeWidth"]);
                    }

                    if (symbolType.Equals("statictext")&& ((string)config["Stroke"] != "#00a2e8"))
                    {
                            util.WriteInYellow(symbolType);
                            file.WriteLine(symbolType);

                            Console.WriteLine("Stroke (old value): " + config["Stroke"] + "; text_value: " + config["StaticText"]);
                            file.WriteLine("Stroke (old value): " + config["Stroke"] + "; text_value: " + config["StaticText"]);

                            config["Stroke"] = "#00a2e8";
                            
                            util.WriteInBlue("Stroke (new Value): " + config["Stroke"]+ "; text_value: " + config["StaticText"]);                          
                            file.WriteLine("Stroke (new Value): " + config["Stroke"] + "; text_value: " + config["StaticText"]);
                        
                    }
                    row["EditorDisplay"] = JsonConvert.SerializeObject(json);                      
                }
            }         
        }

        static public void publishToSQL(DataTable dt, string sqlserver)
        {
            Utilities util = new Utilities();
            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    UpdateSQL(sqlserver, row["EditorDisplay"].ToString(), row["DisplayID"].ToString());
                }
            }
            catch (SqlException ex)
            {
                StringBuilder errorMessages = new StringBuilder();

                for (int i = 0; i < ex.Errors.Count; i++)
                {
                    errorMessages.Append("Index #" + i + "\n" +
                        "Message: " + ex.Errors[i].Message + "\n" +
                        "LineNumber: " + ex.Errors[i].LineNumber + "\n" +
                        "Source: " + ex.Errors[i].Source + "\n");
                }

                util.WriteInRed("Failed to write to the [PIVision].[dbo].[View_Displays] table");
                file.WriteLine("Failed to write to the [PIVision].[dbo].[View_Displays] table");
                util.WriteInRed(errorMessages.ToString());              
                file.WriteLine(errorMessages.ToString());
            }         
        }

        static public void UpdateSQL(string sqlserver,string newEditorDisplayValue, string id)
        {
            string connString = $@"Server={sqlserver};Database=PIVision;Integrated Security=true;MultipleActiveResultSets=true"; /*---> using integrated security*/
            string query = "UPDATE [PIVision].[dbo].[View_Displays] SET [EditorDisplay]=@newString WHERE DisplayID=@Id";

            using (SqlConnection con = new SqlConnection(connString))
            {
                SqlCommand command = new SqlCommand(query, con);
                command.Parameters.Add("@newString", SqlDbType.VarChar).Value = newEditorDisplayValue;
                command.Parameters.Add("@Id", SqlDbType.NVarChar).Value = id;
                con.Open();
                command.ExecuteNonQuery();
                con.Close();
            }
        }
    }
}
