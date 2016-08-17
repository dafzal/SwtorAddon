// Danial Afzal
// iotasquared@gmail.com
using System;
using System.Collections.Generic;
using System.Text;
using Advanced_Combat_Tracker;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;

namespace SwtorAddon
{
    public class SwtorParser : IActPluginV1
    {
        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.BeforeLogLineRead -= ParseLine;
        }

        Regex regex;
        const int DMG = 3, HEALS = 4, THREAT = 16, BUFF = 22;
        public void InitPlugin(System.Windows.Forms.TabPage pluginScreenSpace, System.Windows.Forms.Label pluginStatusText)
        {
            this.SetupSwtorEnvironment();
            ActGlobals.oFormActMain.LogPathHasCharName = false;
            ActGlobals.oFormActMain.LogFileFilter = "*.txt";
            ActGlobals.oFormActMain.ResetCheckLogs();

            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(ParseLine);
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);
            ActGlobals.oFormActMain.LogFileChanged += new LogFileChangedDelegate(oFormActMain_LogFileChanged);
            regex = new Regex(@"\[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \((.*)\)[.<]*([!>]*)[.<]*([!>]*)[>]*", 
                RegexOptions.Compiled);
        }


        private void SetupSwtorEnvironment()
        {
            EncounterData.ExportVariables.Remove("cures");
            EncounterData.ExportVariables.Remove("powerdrain");
            EncounterData.ExportVariables.Remove("powerheal");
            EncounterData.ExportVariables.Remove("maxhealward");
            EncounterData.ExportVariables.Remove("MAXHEALWARD");

            CombatantData.ColumnDefs.Remove("Cures");
            CombatantData.ColumnDefs.Remove("PowerDrain");
            CombatantData.ColumnDefs.Remove("PowerReplenish");
            CombatantData.ColumnDefs["Threat +/-"] = 
                new CombatantData.ColumnDef("Threat +/-", false, "VARCHAR(32)", 
                    "ThreatStr", (Data) => { return Data.GetThreatStr("Threat Done"); }, 
                    (Data) => { return Data.GetThreatStr("Threat Done"); }, 
                    (Left, Right) => { return Left.GetThreatDelta("Threat Done").CompareTo(Right.GetThreatDelta("Threat Done")); });
            CombatantData.ColumnDefs["ThreatDelta"] = 
                new CombatantData.ColumnDef("ThreatDelta", false, "INT", "ThreatDelta", 
                    (Data) => { return Data.GetThreatDelta("Threat Done").ToString(ActGlobals.mainTableShowCommas ? "#,0" : "0"); }, 
                    (Data) => { return Data.GetThreatDelta("Threat Done").ToString(); }, 
                    (Left, Right) => { return Left.GetThreatDelta("Threat Done").CompareTo(Right.GetThreatDelta("Threat Done")); });
            CombatantData.OutgoingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
	        {
		        {"Damage Done", new CombatantData.DamageTypeDef("Damage Done", -1, Color.Orange)},
		        {"Healing Done", new CombatantData.DamageTypeDef("Healing Done", 1, Color.Blue)},
		        {"Threat Done", new CombatantData.DamageTypeDef("Threat Done", 0, Color.Black)},
                //{"Resource Gain", new CombatantData.DamageTypeDef("Resource Gain", 0, Color.DarkBlue)},
                //{"Resource Loss", new CombatantData.DamageTypeDef("Resource Loss", 0, Color.DarkBlue)},
                {"Buffs", new CombatantData.DamageTypeDef("Buffs", 0, Color.Orange)},
                // I dont understand why, but the last entry is always the sum of all other counters. 
                // Its not particularly useful to have a counter for Damage+Threat
		        {"All Outgoing", new CombatantData.DamageTypeDef("All Outgoing", 0, Color.Transparent)} 
	        };
            CombatantData.IncomingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
	        {
		        {"Damage Recieved", new CombatantData.DamageTypeDef("Damage Recieved", -1, Color.Red)},
		        {"Healing Recieved",new CombatantData.DamageTypeDef("Healing Recieved", 1, Color.Brown)},
		        {"Threat Recieved",new CombatantData.DamageTypeDef("Threat Recieved", 0, Color.Yellow)},
		        {"All Incoming",new CombatantData.DamageTypeDef("All Incoming", 0, Color.Transparent)}
	        };
            CombatantData.SwingTypeToDamageTypeDataLinksOutgoing = new SortedDictionary<int, List<string>>
	        { 
		        {DMG, new List<string> { "Damage Done" } },
		        {HEALS, new List<string> { "Healing Done" } },
		        {THREAT, new List<string> { "Threat Done" } },
                //{20, new List<string> { "Resource Gain" } },
                //{21, new List<string> { "Resource Loss" } },
                {BUFF, new List<string>{ "Buffs" } },
	        };
            CombatantData.SwingTypeToDamageTypeDataLinksIncoming = new SortedDictionary<int, List<string>>
	        { 
		        {DMG, new List<string> { "Damage Recieved" } },
		        {HEALS, new List<string> { "Healing Recieved" } },
		        {THREAT, new List<string> { "Threat Recieved" } }
	        };

            CombatantData.DamageSwingTypes = new List<int> { DMG };
            CombatantData.HealingSwingTypes = new List<int> { HEALS };

            CombatantData.DamageTypeDataOutgoingDamage = "Damage Done";
            CombatantData.DamageTypeDataNonSkillDamage = "Damage Done";
            CombatantData.DamageTypeDataOutgoingHealing = "Healing Done";
            CombatantData.DamageTypeDataIncomingDamage = "Damage Recieved";
            CombatantData.DamageTypeDataIncomingHealing = "Healing Recieved";

            CombatantData.ExportVariables.Remove("cures");
            CombatantData.ExportVariables.Remove("maxhealward");
            CombatantData.ExportVariables.Remove("MAXHEALWARD");
            CombatantData.ExportVariables.Remove("powerdrain");
            CombatantData.ExportVariables.Remove("powerheal");

            MasterSwing.ColumnDefs.Remove("Special");

            ActGlobals.oFormActMain.ValidateLists();
            ActGlobals.oFormActMain.ValidateTableSetup();
            ActGlobals.oFormActMain.TimeStampLen = 14;

            // All encounters are set by Enter/ExitCombat.
            UserControl opMainTableGen = (UserControl)ActGlobals.oFormActMain.OptionsControlSets[@"Main Table/Encounters\General"][0];
            CheckBox cbIdleEnd = (CheckBox)opMainTableGen.Controls["cbIdleEnd"];
            cbIdleEnd.Checked = false;
        }

        private class LocalizedName
        {
            public string Value { get; set; }
            public string Id { get; set; }

            private static readonly Regex id_regex = new Regex(@"\s*(.*?)\s*\{(\d+)\}\s*", RegexOptions.Compiled);

            public LocalizedName(string s)
            {
                this.Value = String.Empty;
                this.Id = String.Empty;

                if (!String.IsNullOrEmpty(s))
                {
                    this.Value = s;

                    var m = id_regex.Match(s);
                    if (m.Success)
                    {
                        this.Value = m.Groups[1].Value;
                        this.Id = m.Groups[2].Value;
                    }
                }
            }

            public static LocalizedName Empty
            {
                get { return new LocalizedName(String.Empty); }
            }
        }

        private class LogLine
        {
            public string source;
            public string target;
            public LocalizedName ability;
            public LocalizedName event_type;
            public LocalizedName event_detail;
            public bool crit_value;
            public int value;
            public string value_type;
            public int threat;
            
            static Regex regex = 
                new Regex(@"\[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \((.*)\)[\s<]*(\d*)?[>]*", 
                    RegexOptions.Compiled);
        
            public LogLine(string line) 
            {
                MatchCollection matches = regex.Matches(line);
                source = matches[0].Groups[2].Value;
                target = matches[0].Groups[3].Value;
                ability = new LocalizedName(matches[0].Groups[4].Value);
                if (matches[0].Groups[5].Value.Contains(":"))
                {
                    event_type = new LocalizedName(matches[0].Groups[5].Value.Split(':')[0]);
                    event_detail = new LocalizedName(matches[0].Groups[5].Value.Split(':')[1]);
                }
                else
                {
                    event_type = new LocalizedName(matches[0].Groups[5].Value);
                    event_detail = LocalizedName.Empty;
                }

                crit_value = matches[0].Groups[6].Value.Contains("*");
                string[] raw_value = matches[0].Groups[6].Value.Replace("*", "").Split(' ');
                value = raw_value[0].Length > 0 ? int.Parse(raw_value[0]) : 0;
                if (raw_value.Length > 1)
                {
                    value_type = raw_value[1];
                }
                else
                {
                    value_type = "";
                }
                threat = matches[0].Groups[7].Value.Length > 0 ? int.Parse(matches[0].Groups[7].Value) : 0;
            }
        }

        static DateTime default_date = new DateTime(2012, 1, 1);
        static int last_hour = 0;
        private void ParseLine(bool isImport, LogLineEventArgs log)
        {
            ActGlobals.oFormActMain.GlobalTimeSorter++;
            log.detectedType = Color.Black.ToArgb();
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;
            LogLine line = new LogLine(log.logLine);
            if (log.logLine.Contains("{836045448945490}")) // Exit Combat
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
            if (log.logLine.Contains("{836045448945489}")) // Enter Combat
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                ActGlobals.charName = line.source;
                ActGlobals.oFormActMain.SetEncounter(time, line.source, line.target);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
           
            int type = 0;
            if (log.logLine.Contains("{836045448945501}")) // Damage
            {
                log.detectedType = Color.Red.ToArgb();
                type = DMG;
            }
            else if (log.logLine.Contains("{836045448945488}") || // Taunt
                log.logLine.Contains("{836045448945483}")) // Threat
            {
                log.detectedType = Color.Blue.ToArgb();
                type = THREAT;
            }
            else if (log.logLine.Contains("{836045448945500}")) // Heals
            {
                log.detectedType = Color.Green.ToArgb();
                type = HEALS;
            }
            else if (log.logLine.Contains("{836045448945493}")) // Death
            {
                ActGlobals.oFormActMain.AddCombatAction(DMG, line.crit_value, 
                    "None", line.source, "Killing Blow", Dnum.Death, time,
                    ActGlobals.oFormActMain.GlobalTimeSorter, line.target, "Death");
                
            }
            else if (line.event_type.Id.Equals("836045448945477")) // Buff application
            {
                log.detectedType = Color.Orange.ToArgb();
                type = BUFF;
            }

            /*else if (line.event_type.Contains("Restore"))
            {
                log.detectedType = Color.OrangeRed.ToArgb();
                type = 20;
            }
            else if (line.event_type.Contains("Spend"))
            {
                log.detectedType = Color.Cyan.ToArgb();
                type = 21;
            }
            if (line.ability != "")
            {
                last_ability = line.ability;
            }
            if ((type == 20 || type == 21) && ActGlobals.oFormActMain.SetEncounter(time, line.source, line.target))
            {
                ActGlobals.oFormActMain.AddCombatAction(type, line.crit_value, "None", line.source, last_ability, new Dnum(line.value), time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, "");
            }
            */
            if (!ActGlobals.oFormActMain.InCombat)
            {
                return;
            }
            if (line.threat > 0 && ActGlobals.oFormActMain.SetEncounter(time, line.source, line.target))
            {
                ActGlobals.oFormActMain.AddCombatAction(type, line.crit_value, "None", line.source, line.ability.Value, 
                    new Dnum(line.value), time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, line.value_type);
                ActGlobals.oFormActMain.AddCombatAction(16, line.crit_value, "None", line.source, line.ability.Value, 
                    new Dnum(line.threat), time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, "Increase");
            }
            if (type == BUFF && ActGlobals.oFormActMain.SetEncounter(time, line.source, line.target))
            {
                ActGlobals.oFormActMain.AddCombatAction(type, line.crit_value, "None", line.source, line.event_detail.Value,
                    Dnum.NoDamage, time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, line.value_type);
            }
            return;
        }
        DateTime logFileDate = DateTime.Now;
        Regex logfileDateTimeRegex = new Regex(@"combat_(?<Y>\d{4})-(?<M>\d\d)-(?<D>\d\d)_(?<h>\d\d)_(?<m>\d\d)_(?<s>\d\d)_\d+\.txt", RegexOptions.Compiled);
        void oFormActMain_LogFileChanged(bool IsImport, string NewLogFileName)
        {
            if (NewLogFileName == "")
            {
                return;
            }
            //combat_2012-04-02_09_20_30_162660.txt
            FileInfo newFile = new FileInfo(NewLogFileName);
            Match match = logfileDateTimeRegex.Match(newFile.Name);
            if (match.Success)	// If we can parse the creation date from the filename
            {
                try
                {
                    logFileDate = new DateTime(
                        Int32.Parse(match.Groups[1].Value),		// Y
                        Int32.Parse(match.Groups[2].Value),		// M
                        Int32.Parse(match.Groups[3].Value),		// D
                        Int32.Parse(match.Groups[4].Value),		// h
                        Int32.Parse(match.Groups[5].Value),		// m
                        Int32.Parse(match.Groups[6].Value));		// s
                }
                catch
                {
                    logFileDate = newFile.CreationTime;
                }
            }
            else
            {
                logFileDate = newFile.CreationTime;
            }
        }
        private DateTime ParseDateTime(string line)
        {
            try
            {
                //[22:55:28.335] 
                if (line.Length < ActGlobals.oFormActMain.TimeStampLen)
                    return ActGlobals.oFormActMain.LastEstimatedTime;

                int hour, min, sec, millis;

                hour = Convert.ToInt32(line.Substring(1, 2));
                min = Convert.ToInt32(line.Substring(4, 2));
                sec = Convert.ToInt32(line.Substring(7, 2));
                millis = Convert.ToInt32(line.Substring(10, 3));
                DateTime parsedTime = new DateTime(logFileDate.Year, logFileDate.Month, logFileDate.Day, hour, min, sec, millis);
                if (parsedTime < logFileDate)			// if time loops from 23h to 0h, the parsed time will be less than the log creation time, so add one day
                    parsedTime = parsedTime.AddDays(1);	// only works for log files that are less than 24h in duration

                return parsedTime;
            }
            catch
            {
                return ActGlobals.oFormActMain.LastEstimatedTime;
            }
        }
    }
}
