// Danial Afzal
// iotasquared@gmail.com
using System;
using System.Collections.Generic;
using System.Linq;
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
        public void InitPlugin(System.Windows.Forms.TabPage pluginScreenSpace, System.Windows.Forms.Label pluginStatusText)
        {
            this.SetupSwtorEnvironment();
            ActGlobals.oFormActMain.LogPathHasCharName = false;
            ActGlobals.oFormActMain.LogFileFilter = "*.txt";
            ActGlobals.oFormActMain.ResetCheckLogs();

            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(ParseLine);
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);
            regex = new Regex(@"(?<=[\[<\(])[^\[<\(\]>\)]*(?=[\]>\)])", RegexOptions.Compiled);
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

            CombatantData.OutgoingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
	        {
		        {"Damage Done", new CombatantData.DamageTypeDef("Damage Done", -1, Color.Black)},
		        {"Healing Done", new CombatantData.DamageTypeDef("Healing Done", 1, Color.Blue)},
		        {"Threat Done", new CombatantData.DamageTypeDef("Threat Done", 0, Color.Orange)},
                // I dont understand why, but the last entry is always the sum of all other counters. 
                // Its not particularly useful to have a counter for Damage+Threat
		        {"", new CombatantData.DamageTypeDef("", 0, Color.Transparent)} 
	        };
            CombatantData.IncomingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
	        {
		        {"Damage Recieved", new CombatantData.DamageTypeDef("Damage Recieved", -1, Color.Red)},
		        {"Healing Recieved",new CombatantData.DamageTypeDef("Healing Recieved", 1, Color.Brown)},
		        {"Threat Recieved",new CombatantData.DamageTypeDef("Threat Recieved", 0, Color.Yellow)},
		        {" ",new CombatantData.DamageTypeDef(" ", 0, Color.Transparent)}
	        };
            CombatantData.SwingTypeToDamageTypeDataLinksOutgoing = new SortedDictionary<int, List<string>>
	        { 
		        {2, new List<string> { "Damage Done" } },
		        {3, new List<string> { "Healing Done" } },
		        {16, new List<string> { "Threat Done" } }
	        };
            CombatantData.SwingTypeToDamageTypeDataLinksIncoming = new SortedDictionary<int, List<string>>
	        { 
		        {2, new List<string> { "Damage Recieved" } },
		        {3, new List<string> { "Healing Recieved" } },
		        {16, new List<string> { "Threat Recieved" } }
	        };

            CombatantData.DamageSwingTypes = new List<int> { 2 };
            CombatantData.HealingSwingTypes = new List<int> { 3 };

            CombatantData.DamageTypeDataOutgoingDamage = "Damage Done";
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
        }

        private void ParseLine(bool isImport, LogLineEventArgs log)
        {
            ActGlobals.oFormActMain.GlobalTimeSorter++;
            string line = log.logLine;
            MatchCollection matches = regex.Matches(line);
            string source = matches[1].Value;
            string target = matches[2].Value;
            string ability = matches[3].Value;
            string event_type, event_detail;
            if (matches[4].Value.Contains(':'))
            {
                event_type = matches[4].Value.Split(':')[0];
                event_detail = matches[4].Value.Split(':')[1];
            }
            else
            {
                event_type = matches[4].Value;
                event_detail = "";
            }

            bool crit_value = matches[5].Value.Contains('*');
            string[] raw_value = matches[5].Value.Replace("*", "").Split(' ');
            int value = raw_value[0].Length > 0 ? int.Parse(raw_value[0]) : 0;
            string value_type;
            if (raw_value.Length > 1)
            {
                value_type = raw_value[1];
            }
            else
            {
                value_type = "";
            }

            int threat;
            if (matches.Count >= 7)
            {
                threat = matches[6].Value.Length > 0 ? int.Parse(matches[6].Value) : 0;
            }
            else
            {
                threat = 0;
            }

            // -------handle data--------

            log.detectedType = Color.Black.ToArgb();
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;

            if (event_detail.Contains("ExitCombat"))
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
            if (event_detail.Contains("EnterCombat"))
            {
                ActGlobals.oFormActMain.SetEncounter(time, source, target);
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
           
            int type = 0;
            if (event_detail.Contains("Damage"))
            {
                log.detectedType = Color.Red.ToArgb();
                type = 2;
            }
            else if (event_detail.Contains("Taunt") || event_detail.Contains("Threat"))
            {
                log.detectedType = Color.Blue.ToArgb();
                type = 16;
            }
            else if (event_detail.Contains("Heal"))
            {
                log.detectedType = Color.Green.ToArgb();
                type = 3;
            }
            // Heat/Rage/Force regen in this game is weird. Spend and Restore mean different things based on the class.
            /*else if (event_type.Contains("Spend"))
            {
                log.detectedType = Color.OrangeRed.ToArgb();
                type = SwingTypeEnum.PowerDrain;
            }
            else if (event_type.Contains("Restore"))
            {
                log.detectedType = Color.OrangeRed.ToArgb();
                type = SwingTypeEnum.PowerHealing;
            }
            else
            {
                type = SwingTypeEnum.Melee;
            }*/
            if (threat > 0 && ActGlobals.oFormActMain.SetEncounter(time, source, target))
            {
                ActGlobals.oFormActMain.AddCombatAction(type, crit_value, "None", source, ability, new Dnum(value), time, ActGlobals.oFormActMain.GlobalTimeSorter, target, value_type);
                ActGlobals.oFormActMain.AddCombatAction(16, crit_value, "None", source, ability, new Dnum(threat), time, ActGlobals.oFormActMain.GlobalTimeSorter, target, "Increase");
            }
            return;

        }
        private DateTime ParseDateTime(string line)
        {
            try
            {
                return DateTime.Parse(line.Substring(1, 19));
            }
            catch
            {
                return ActGlobals.oFormActMain.LastEstimatedTime;
            }
        }
    }
}
