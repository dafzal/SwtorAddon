// Danial Afzal
// iotasquared@gmail.com
using System;
using System.Collections.Generic;
using System.Text;
using Advanced_Combat_Tracker;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;

namespace SwtorAddon
{
    public class SwtorParser : IActPluginV1
    {
        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.BeforeLogLineRead -= ParseLine;
        }

        Regex regex;
        const int DMG = 3, HEALS = 4, THREAT = 16;
        public void InitPlugin(System.Windows.Forms.TabPage pluginScreenSpace, System.Windows.Forms.Label pluginStatusText)
        {
            this.SetupSwtorEnvironment();
            ActGlobals.oFormActMain.LogPathHasCharName = false;
            ActGlobals.oFormActMain.LogFileFilter = "*.txt";
            ActGlobals.oFormActMain.ResetCheckLogs();

            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(ParseLine);
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);
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
	        };
            CombatantData.SwingTypeToDamageTypeDataLinksIncoming = new SortedDictionary<int, List<string>>
	        { 
		        {DMG, new List<string> { "Damage Recieved" } },
		        {HEALS, new List<string> { "Healing Recieved" } },
		        {THREAT, new List<string> { "Threat Recieved" } }
	        };

            CombatantData.DamageSwingTypes = new List<int> { 2 };
            CombatantData.HealingSwingTypes = new List<int> { 3 };

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
            ActGlobals.oFormActMain.TimeStampLen = 22;

            // All encounters are set by Enter/ExitCombat.
            UserControl opMainTableGen = (UserControl)ActGlobals.oFormActMain.OptionsControlSets[@"Main Table/Encounters\General"][0];
            CheckBox cbIdleEnd = (CheckBox)opMainTableGen.Controls["cbIdleEnd"];
            cbIdleEnd.Checked = false;
        }

        private class LogLine
        {
            public string source;
            public string target;
            public string ability;
            public string event_type, event_detail;
            public bool crit_value;
            public int value;
            public string value_type;
            public int threat;
            
            static Regex regex = 
                new Regex(@"\[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \[(.*)\] \((.*)\)[.<]*([!>]*)[\s<]*(\d*)?[>]*", 
                    RegexOptions.Compiled);
            static Regex id_regex = new Regex(@"\s*\{\d*}\s*", RegexOptions.Compiled);
            public LogLine(string line) 
            {
                line = id_regex.Replace(line, "");
                MatchCollection matches = regex.Matches(line);
                source = matches[0].Groups[2].Value;
                target = matches[0].Groups[3].Value;
                ability = matches[0].Groups[4].Value;
                if (matches[0].Groups[5].Value.Contains(":"))
                {
                    event_type = matches[0].Groups[5].Value.Split(':')[0];
                    event_detail = matches[0].Groups[5].Value.Split(':')[1].Trim();
                }
                else
                {
                    event_type = matches[0].Groups[5].Value;
                    event_detail = "";
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
                threat = matches[0].Groups[8].Value.Length > 0 ? int.Parse(matches[0].Groups[8].Value) : 0;
            }
        }

        private void ParseLine(bool isImport, LogLineEventArgs log)
        {
            ActGlobals.oFormActMain.GlobalTimeSorter++;
            log.detectedType = Color.Black.ToArgb();
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;
            LogLine line = new LogLine(log.logLine);
            if (line.event_detail.Contains("ExitCombat"))
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
            if (line.event_detail.Contains("EnterCombat"))
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                ActGlobals.oFormActMain.SetEncounter(time, line.source, line.target);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
           
            int type = 0;
            if (line.event_detail.Contains("Damage"))
            {
                log.detectedType = Color.Red.ToArgb();
                type = DMG;
            }
            else if (line.event_detail.Contains("Taunt") || line.event_detail.Contains("Threat"))
            {
                log.detectedType = Color.Blue.ToArgb();
                type = THREAT;
            }
            else if (line.event_detail.Contains("Heal"))
            {
                log.detectedType = Color.Green.ToArgb();
                type = HEALS;
            }
            else if (line.event_detail.Contains("Death"))
            {
                ActGlobals.oFormActMain.AddCombatAction((int)SwingTypeEnum.NonMelee, line.crit_value, 
                    "None", line.source, line.ability, Dnum.Death, time,
                    ActGlobals.oFormActMain.GlobalTimeSorter, line.target, "Death");
                
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
                ActGlobals.oFormActMain.AddCombatAction(type, line.crit_value, "None", line.source, line.ability, 
                    new Dnum(line.value), time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, line.value_type);
                ActGlobals.oFormActMain.AddCombatAction(16, line.crit_value, "None", line.source, line.ability, 
                    new Dnum(line.threat), time, ActGlobals.oFormActMain.GlobalTimeSorter, line.target, "Increase");
            }
            return;
        }
        private DateTime ParseDateTime(string line)
        {
            try
            {
                //[03/16/2012 22:55:28] 
                if (line.Length < ActGlobals.oFormActMain.TimeStampLen)
                    return ActGlobals.oFormActMain.LastEstimatedTime;

                int year, month, day, hour, min, sec;
                month = Convert.ToInt32(line.Substring(1, 2));
                day = Convert.ToInt32(line.Substring(4, 2));
                year = Convert.ToInt32(line.Substring(7, 4));
                hour = Convert.ToInt32(line.Substring(12, 2));
                min = Convert.ToInt32(line.Substring(15, 2));
                sec = Convert.ToInt32(line.Substring(18, 2));

                return new DateTime(year, month, day, hour, min, sec);
            }
            catch
            {
                return ActGlobals.oFormActMain.LastEstimatedTime;
            }
        }
    }
}
