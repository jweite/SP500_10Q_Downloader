using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using AngleSharp;
using System.Text.RegularExpressions;


namespace Wikipedia_SP500_10Q_Downloader
{
    class Program
    {
        static void Main(string[] args)
        {
            // Fetch Wikipedia's SP500 page
            string wikipediaSp500URL = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies";
            WebClient webClient = new WebClient();
            string wikipediaSp500page = webClient.DownloadString(wikipediaSp500URL);

            // Parse its table of SP500 members
            // As of today it's the first table in the page.  The data of interest is in its tbody.
            var angleSharpConfig = Configuration.Default.WithCss();
            var parser = new AngleSharp.Parser.Html.HtmlParser(angleSharpConfig);
            var wikipediaSp500Doc = parser.Parse(wikipediaSp500page);
            var table = wikipediaSp500Doc.QuerySelector("table");

            // Extract the table headings
            var headings = table.QuerySelectorAll("th").Select(th => th.TextContent).ToArray<string>();

            // Extract the table data
            IDictionary<string, IDictionary<string, string>> sp500 = new Dictionary<string, IDictionary<string, string>>();

            // Iterate over its rows, parsing each row into a Dictionary of its cells, and assembling a Dictionary of those row Dictionaries keyed by ticker
            var tableRows = table.QuerySelectorAll("tr");
            foreach (var tr in tableRows)
            {
                IDictionary<string, string> rowDict = new Dictionary<string, string>();
                for (int cellIndex = 0; cellIndex < tr.Children.Length; ++cellIndex)
                {
                    rowDict.Add(headings[cellIndex], tr.Children[cellIndex].TextContent);
                }
                if (rowDict["CIK"] != "CIK")        // Skip the heading row
                {
                    sp500.Add(rowDict["Ticker symbol"], rowDict);
                }
            }

            // Iterate over the companies of the SP500
            foreach(var company in sp500.Values)
            {
                string cik = company["CIK"];

                // Retrieve/parse the current company's Edgar page, limiting filings to 10-Q's
                string edgarCompanyPageURL = String.Format("http://www.sec.gov/cgi-bin/browse-edgar?CIK={0}&action=getcompany&type=10-Q", cik);
                string edgarCompanyPage = webClient.DownloadString(edgarCompanyPageURL);
                var edgarCompanyPageDoc = parser.Parse(edgarCompanyPage);

                // Find the current company's filings in their Edgar page.
                var edgarCompanyFilingsTable = edgarCompanyPageDoc.QuerySelector(".tableFile2");
                var edgarCompanyFilingRows = edgarCompanyFilingsTable.QuerySelectorAll("tr");

                // Look for their first (most recent) 10-Q filing row.  (Instead, maybe just take row 1...)
                int iRow = 0;
                for (iRow = 1; iRow < edgarCompanyFilingRows.Length; ++iRow)    // Skip the heading
                {
                    var cells = edgarCompanyFilingRows[iRow].QuerySelectorAll("td");
                    if (cells[0].TextContent == "10-Q")
                    {
                        break;
                    }
                }
                if (iRow >= edgarCompanyFilingRows.Length)
                {
                    Console.WriteLine("Cannot find 10-Q filing row in " + edgarCompanyPageURL);
                    continue;
                }
                var firstTenQrow = edgarCompanyFilingRows[iRow];

                // Retrieve that that filing's page and parse it.
                var tenQPageAnchor = firstTenQrow.QuerySelector("#documentsbutton");
                var tenQPageUri = tenQPageAnchor.GetAttribute("href");
                string tenQPageUrl = "https://www.sec.gov/" + tenQPageUri;
                string tenQPage = webClient.DownloadString(tenQPageUrl);
                var tenQPageDoc = parser.Parse(tenQPage);

                // Get the rows of the filing's files table
                var tenQFilesTable = tenQPageDoc.QuerySelector(".tableFile");
                var tenQFilesTableRows = tenQFilesTable.QuerySelectorAll("tr");

                // Look for the actual 10 - Q html file
                for (iRow = 1; iRow < tenQFilesTableRows.Length; ++iRow)    // Skip the heading
                {
                    var cells = tenQFilesTableRows[iRow].QuerySelectorAll("td");
                    if (cells[3].TextContent == "10-Q")
                    {
                        break;
                    }
                }
                if (iRow >= tenQFilesTableRows.Length)
                {
                    Console.WriteLine("Cannot find a file row with Description of 10-Q for " + company["Security"] + " in " + in " + tenPageQurl);
                    continue;
                }
                var tenQrow = tenQFilesTableRows[iRow];

                // Extract the link to the 10-Q html file
                var tenQAnchor = tenQrow.QuerySelector("a");
                var tenQUri = tenQAnchor.GetAttribute("href");
                var tenQUrl = "https://www.sec.gov/" + tenQUri;

                // Download the 10-Q html file and save it locally
                string tenQ = webClient.DownloadString(tenQUrl);
                File.WriteAllText(@"c:\temp\Wikipedia_SP500_10Q_Downloader_Output\" + company["Ticker symbol"] + "_" + tenQAnchor.TextContent, tenQ);

                Console.WriteLine("Downloaded " + tenQAnchor.TextContent + " for " + company["Security"]);
            }
            Console.WriteLine("Done");
        }
    }
}
