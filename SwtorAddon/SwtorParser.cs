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
        }

        Regex regex;
        public void InitPlugin(System.Windows.Forms.TabPage pluginScreenSpace, System.Windows.Forms.Label pluginStatusText)
        {
            ActGlobals.oFormActMain.SetParserToNull();
            ActGlobals.oFormActMain.LogPathHasCharName = false;
            ActGlobals.oFormActMain.LogFileFilter = "*.txt";
            ActGlobals.oFormActMain.ResetCheckLogs();

            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(ParseLine);
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);
            regex = new Regex(@"(?<=[\[<\(])[^\[<\(\]>\)]*(?=[\]>\)])");
        }
        bool inCombat = false;
        void ParseLine(bool isImport, LogLineEventArgs log)
        {
            ActGlobals.oFormActMain.GlobalTimeSorter++;
            string line = log.logLine;
            MatchCollection matches = regex.Matches(line);
            string source = matches[1].Value;
            string target = matches[2].Value;
            string ability = matches[3].Value;
            string event_type, event_detail;
            if (matches[4].Value.Contains(':')) {
                event_type = matches[4].Value.Split(':')[0];
                event_detail = matches[4].Value.Split(':')[1];
            } else {
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
            if (matches.Count >= 7) {
                threat = matches[6].Value.Length > 0 ? int.Parse(matches[6].Value) : 0;
            } else {
                threat = 0;
            }

            // -------handle data--------
            
            log.detectedType = Color.Black.ToArgb();
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;
            
            if (event_detail.Contains("ExitCombat"))
            {
                ActGlobals.oFormActMain.EndCombat(!isImport);
                inCombat = false;
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
            if (event_detail.Contains("EnterCombat"))
            {
                ActGlobals.oFormActMain.SetEncounter(time, source, target);
                inCombat = true;
                log.detectedType = Color.Purple.ToArgb();
                return;
            }
            if (!inCombat)
            {
                return;
            }
            SwingTypeEnum type = SwingTypeEnum.NonMelee;
            if (event_detail.Contains("Damage"))
            {
                log.detectedType = Color.Red.ToArgb();
                type = SwingTypeEnum.NonMelee;
            }
            else if (event_detail.Contains("Taunt") || event_detail.Contains("Threat"))
            {
                log.detectedType = Color.Blue.ToArgb();
                type = SwingTypeEnum.Threat;
            }
            else if (event_detail.Contains("Heal"))
            {
                log.detectedType = Color.Green.ToArgb();
                type = SwingTypeEnum.Healing;
            }
            else if (event_type.Contains("Spend"))
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
            }
            ActGlobals.oFormActMain.AddCombatAction((int)type, crit_value, "None", source, ability, new Dnum(value), time, ActGlobals.oFormActMain.GlobalTimeSorter, target, value_type);
            if (threat > 0)
            {
                ActGlobals.oFormActMain.AddCombatAction((int)SwingTypeEnum.Threat, crit_value, "None", source, ability, new Dnum(threat), time, ActGlobals.oFormActMain.GlobalTimeSorter, target, "");
            } 
            return;

        }
        private DateTime ParseDateTime(string line)
        {
            try
            {
                MatchCollection matches = regex.Matches(line);
                return DateTime.Parse(matches[0].Value);
            }
            catch
            {
                return ActGlobals.oFormActMain.LastEstimatedTime;
            }
        }
    }
}
