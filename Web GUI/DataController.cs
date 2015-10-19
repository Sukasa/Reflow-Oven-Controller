using System;
using System.Text;
using Microsoft.SPOT;
using Rinsen.WebServer;

namespace Reflow_Oven_Controller.Web_GUI
{
    class DataController : Controller
    {
        public void Index()
        {
            StringBuilder SB = new StringBuilder("{\"OvenTemperature\":");
            SB.Append(OvenController.OvenTemperature.ToString());

            SB.Append(",\"BayTemperature\":");
            SB.Append(OvenController.BayTemperature.ToString());

            SB.Append(",\"FreeMem\":");
            SB.Append(OvenController.FreeMem.ToString());

            SB.Append(",\"DoorAjar\":");
            SB.Append((OvenController.DoorAjar ? 1 : 0).ToString());

            SB.Append(",\"LowerPower\":");
            SB.Append(OvenController.LowerElementPower.ToString());

            SB.Append(",\"UpperPower\":");
            SB.Append(OvenController.UpperElementPower.ToString());

            SB.Append(",\"Load\":");
            SB.Append(OvenController.CPULoad.LoadPercentage.ToString());

            SB.Append(",\"Setpoint\":");
            SB.Append(OvenController.TemperatureSetpoint.ToString());

            SB.Append(",\"MaxBayTemperature\":");
            SB.Append(OvenController.MaxBayTemperature.ToString());
                
            SB.Append(",\"Faults\":");
            SB.Append(OvenController.Faults.ToString());

            SB.Append(",\"TSense1\":");
            SB.Append(OvenController.Sensor1.HotTemp.ToString());

            SB.Append(",\"TSense2\":");
            SB.Append(OvenController.Sensor2.HotTemp.ToString());

            SB.Append(",\"ElementsEnabled\":");
            SB.Append((OvenController.ElementsEnabled ? 1 : 0).ToString());

            SB.Append(",\"Fan2\":");
            SB.Append(OvenController.OvenFanSpeed.ToString());

            SB.Append(",\"State\": \"");
            SB.Append(OvenController.ProfileController.CurrentState.ToString());

            SB.Append("\"}");
            SetHtmlResult(SB.ToString());
        }
    }
}
