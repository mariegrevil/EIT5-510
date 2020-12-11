// https://docs.microsoft.com/en-us/dotnet/api/system.io.ports.serialport?view=dotnet-plat-ext-5.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;

public class PortChat
{
    static bool _run;       // Ready to run the program?
    static SerialPort _serialPort;

    const int actArrayLength = 10;

    public static void Main()
    {
        string readXbee;
        StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;

        _run = false;

        // Create a new SerialPort object with default settings.
        _serialPort = new SerialPort();

        while (!_run)
        {
            // Print the current SerialPort settings.
            PortSettings(_serialPort);
            readXbee = Console.ReadLine();     // Read user input.
            Console.WriteLine();

            // What to do if user readXbees "start"?
            if (stringComparer.Equals("start", readXbee))
            {
                _run = true;                // Exit the settings loop in order to run the program.
                //Console.WriteLine();
            }
            else if (stringComparer.Equals("port", readXbee))
            {
                SetPortName(_serialPort);
            }
            else if (stringComparer.Equals("baud", readXbee))
            {
                SetBaudRate(_serialPort);
            }
            else if (stringComparer.Equals("pari", readXbee))
            {
                SetParity(_serialPort);
            }
            else if (stringComparer.Equals("data", readXbee))
            {
                SetDataBits(_serialPort);
            }
            else if (stringComparer.Equals("stop", readXbee))
            {
                SetStopBits(_serialPort);
            }
            else if (stringComparer.Equals("hand", readXbee))
            {
                SetHandshake(_serialPort);
            }
            //Console.WriteLine();
        }

        Thread readThread = new Thread(Read);

        // Set the read/write timeouts
        _serialPort.ReadTimeout = 500;
        _serialPort.WriteTimeout = 500;

        _serialPort.Open();
        readThread.Start();

        Console.WriteLine("Time to run the program...");

        readThread.Join();
        _serialPort.Close();
    }

    // The maximum size of the string being sent to Arduino. Must be the same as in Arduino code.
    const int valueLength = 10;

    const int numLED = 11;
    static double[] sensorAngleFollow = new double[numLED];
    static double sensorSpeedFollow;
    //double sensorSpeedLeader;

    // Current time [0] and previous time [1].
    const int timeSamples = 3; // How many timestamps do we need for calculations?
    static int timeCount = 0; // How many timestamps have been filled in so far?
    static long[] time = new long[timeSamples];
    static bool timeFilled = false; // Has the timestamp array been filled?

    // Calculated values to be returned.
    static double actuatorAngleFollow;
    static bool actuatorAngleFollowReturn = false;
    static double actuatorSpeedFollow;
    static bool actuatorSpeedFollowReturn = false;

    // Read and treat data.
    public static void Read()
    {
        // Fill the timestamp array with 0.
        /*for (int i = 0; i < timeSamples; i++)
        {
            time[i] = 0;
        }*/
        string input = _serialPort.ReadLine();
        Console.WriteLine(input);
        string output;
        string pattern = "[A-Z]([0-9]+([.][0-9]+)?[/]?)*[&]";
        string identityPattern = "^[A-Z]+";
        string valuePattern = "([0-9]+([.][0-9]+)?[/]?)+";
        string splitPattern = "[/]";
        Console.WriteLine(input);
        while (_run)
        {
            try
            {

                ////////////////////////////////////////////////////////
                // DECIPHER STRING                                    //
                ////////////////////////////////////////////////////////

                // Check every pattern match for an identifier and a value.
                foreach (Match match in Regex.Matches(input, pattern))
                {
                    string identity;
                    string value;
                    //Console.WriteLine(match.Value);

                    // Run if an identifier is found.
                    Match identityMatch = Regex.Match(match.Value, identityPattern);
                    if (identityMatch.Success)
                    {
                        identity = identityMatch.Value;
                        //Console.WriteLine(identity);
                        //Console.WriteLine(match.Value);

                        // Run if a value is found.
                        Match valueMatch = Regex.Match(match.Value, valuePattern);
                        if (valueMatch.Success)
                        {
                            value = valueMatch.Value;
                            //Console.WriteLine(valueMatch.Value);
                            //Console.WriteLine(value);

                            // Both conditions are true, so let's save the value to its appropriate object.
                            switch (identity)
                            {
                                ////////////////////////////////////////////////////////
                                // MAKE SURE EACH SENSOR VALUE IS STORED CORRECTLY    //
                                ////////////////////////////////////////////////////////
                                // LED data.
                                case "L":
                                    int counterLED = 0;
                                    foreach (string item in Regex.Split(value, splitPattern))
                                    {
                                        if (counterLED < numLED)
                                        {
                                        sensorAngleFollow[counterLED] = Convert.ToDouble(item);
                                        counterLED++;
                                        }
                                    }
                                    actuatorAngleFollowReturn = true;
                                    break;
                                case "M":
                                    sensorSpeedFollow = Convert.ToDouble(value);
                                    actuatorSpeedFollowReturn = true;
                                    break;
                                //case "X":
                                    //for (int i = 0; i < timeSamples - 1; i++)
                                    //{
                                    //    time[i + 1] = time[i];
                                    //}
                                    //time[1] = time[0];
                                    //time[0] = Convert.ToInt64(value);
                                    //timeCount++;
                                    //if (!timeFilled)
                                    //{
                                    //    if (timeCount >= timeSamples)
                                    //    {
                                    //        timeFilled = true;
                                    //    }                                    }
                                    //break;
                                default:
                                    break;
                            }
                        }
                    }
                }

                //for (int i = 0; i < numLED; i++)
                //{
                //    Console.WriteLine(sensorAngleFollow[i]);
                //}
                //Console.WriteLine(sensorSpeedFollow);

                ////////////////////////////////////////////////////////
                // CALCULATIONS                                       //
                ////////////////////////////////////////////////////////

                // Placeholder angle calculation.
                actuatorAngleFollow = 0;
                foreach (double reading in sensorAngleFollow)
                {
                    //actuatorAngleFollow += reading;
                    actuatorAngleFollow = IRFeedback();
                }
                actuatorAngleFollow = actuatorAngleFollow / numLED;

                // Placeholder speed calculation.
                actuatorSpeedFollow = sensorSpeedFollow / 2;

                ////////////////////////////////////////////////////////
                // PACKAGE THE CALCULATED VALUES                      //
                ////////////////////////////////////////////////////////

                // Begin.
                output = "?";
                // Package angle value.
                //Console.WriteLine(actuatorAngleFollowReturn);
                if (actuatorAngleFollowReturn)
                {
                    //Console.WriteLine("WOOOH");
                    output += "A";
                    output += doubleToString(actuatorAngleFollow, 10);
                    output += "&";
                    actuatorAngleFollowReturn = false;
                }
                // Package angle value.
                if (actuatorSpeedFollowReturn)
                {
                    output += "B";
                    output += doubleToString(actuatorSpeedFollow, 10);
                    output += "&";
                    actuatorSpeedFollowReturn = false;
                }
                // Wrap it up.
                output += "!";

                ////////////////////////////////////////////////////////
                // RETURN CALCULATED VALUES                           //
                ////////////////////////////////////////////////////////

                Console.WriteLine(output);
                _serialPort.WriteLine(output);
            }
            catch (TimeoutException) { }
        }
    }

    public static string doubleToString(double value, int maxLength)
    {
        int length = maxLength;
        string content = Convert.ToString(value);
        if (content.Length < length)
        {
            length = content.Length;
        }
        return content.Substring(0, length);
    }

    // Print current SerialPort settings.
    public static void PortSettings(SerialPort varSerialPort)
    {
        Console.WriteLine("PORT\tPort name:\t" + varSerialPort.PortName);
        Console.WriteLine("BAUD\tBaud rate:\t" + varSerialPort.BaudRate);
        Console.WriteLine("PARI\tParity   :\t" + varSerialPort.Parity);
        Console.WriteLine("DATA\tData bits:\t" + varSerialPort.DataBits);
        Console.WriteLine("STOP\tStop bits:\t" + varSerialPort.StopBits);
        Console.WriteLine("HAND\tHandshake:\t" + varSerialPort.Handshake);
        Console.WriteLine("\r\nType START to run the program.\r\n");
    }

    // Display available ports and let the user select one.
    public static void SetPortName(SerialPort varSerialPort)
    {
        bool _select = false;
        bool _intro = false;
        string portId;
        int numOpts = SerialPort.GetPortNames().Length;
        string[] portOpts = new string[numOpts];
        int id = 0;
        foreach (string port in SerialPort.GetPortNames())
        {
            portOpts[id] = port;
            id++;
        }
        while (!_select)
        {
            if (!_intro)
            {
                Console.WriteLine("Available port options:");
                for (int i = 0; i < numOpts; i++)
                {
                    Console.Write("({0})\t", i);
                    Console.WriteLine("{0}", portOpts[i]);
                }
                _intro = true;
                Console.WriteLine();
            }
            Console.Write("Select port option: ");
            portId = Console.ReadLine();
            if (portId == "")
            {
                _select = true;
            }
            else
            {
                if (int.Parse(portId) < numOpts)
                {
                    _select = true;
                }
                if (!_select)
                {
                    Console.WriteLine(portId + " is not a valid port option.");
                }
                else
                {
                    varSerialPort.PortName = portOpts[int.Parse(portId)];
                }
            }
            Console.WriteLine();
        }
    }

    // Display BaudRate values and prompt user to enter a value.
    public static void SetBaudRate(SerialPort varSerialPort)
    {
        bool _select = false;
        string baudRate;
        while (!_select)
        {
            Console.Write("Enter baud rate: ");
            baudRate = Console.ReadLine();
            if (baudRate == "")
            {
                _select = true;
            }
            else
            {
                _select = true;
                varSerialPort.BaudRate = int.Parse(baudRate);
            }
            Console.WriteLine();
        }
    }

    // Display PortParity values and prompt user to enter a value.
    public static void SetParity(SerialPort varSerialPort)
    {
        bool _select = false;
        bool _intro = false;
        string parityId;
        int numOpts = Enum.GetNames(typeof(Parity)).Length;
        string[] parityOpts = new string[numOpts];
        int id = 0;
        foreach (string parity in Enum.GetNames(typeof(Parity)))
        {
            parityOpts[id] = parity;
            id++;
        }
        while (!_select)
        {
            if (!_intro)
            {
                Console.WriteLine("Available parity options:");
                for (int i = 0; i < numOpts; i++)
                {
                    Console.Write("({0})\t", i);
                    Console.WriteLine("{0}", parityOpts[i]);
                }
                _intro = true;
                Console.WriteLine();
            }
            Console.Write("Select parity option: ");
            parityId = Console.ReadLine();
            if (parityId == "")
            {
                _select = true;
            }
            else
            {
                if (int.Parse(parityId) < numOpts)
                {
                    _select = true;
                }
                if (!_select)
                {
                    Console.WriteLine(parityId + " is not a valid parity option.");
                }
                else
                {
                    varSerialPort.Parity = (Parity)Enum.Parse(typeof(Parity), parityOpts[int.Parse(parityId)], true);//parityOpts[int.Parse(parityId)];
                }
            }
            Console.WriteLine();
        }
    }
    // Display DataBits values and prompt user to enter a value.
    public static void SetDataBits(SerialPort varSerialPort)
    {
        bool _select = false;
        string dataBits;
        while (!_select)
        {
            Console.Write("Enter number of data bits: ");
            dataBits = Console.ReadLine();
            if (dataBits == "")
            {
                _select = true;
            }
            else if ((int.Parse(dataBits) > 8) || (int.Parse(dataBits) < 5))
            {
                Console.WriteLine(dataBits + " is not a valid data bit option. The value must be between 5 and 8.");
            }
            else
            {
                _select = true;
                varSerialPort.DataBits = int.Parse(dataBits);
            }
            Console.WriteLine();
        }
    }

    // Display StopBits values and prompt user to enter a value.
    public static void SetStopBits(SerialPort varSerialPort)
    {
        bool _select = false;
        bool _intro = false;
        string stopId;
        int numOpts = Enum.GetNames(typeof(StopBits)).Length;
        string[] stopOpts = new string[numOpts];
        int id = 0;
        foreach (string parity in Enum.GetNames(typeof(StopBits)))
        {
            stopOpts[id] = parity;
            id++;
        }
        while (!_select)
        {
            if (!_intro)
            {
                Console.WriteLine("Available stop bit options:");
                for (int i = 0; i < numOpts; i++)
                {
                    if (!(stopOpts[i] == "None"))
                    {
                        Console.Write("({0})\t", i);
                        Console.WriteLine("{0}", stopOpts[i]);
                    }
                }
                _intro = true;
                Console.WriteLine();
            }
            Console.Write("Select stop bit option: ");
            stopId = Console.ReadLine();
            if (stopId == "")
            {
                _select = true;
            }
            else
            {
                if (int.Parse(stopId) < numOpts)
                {
                    _select = true;
                }
                if (!_select)
                {
                    Console.WriteLine(stopId + " is not a valid stop bit option.");
                }
                else
                {
                    varSerialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopOpts[int.Parse(stopId)], true);
                }
            }
            Console.WriteLine();
        }
    }

    public static void SetHandshake(SerialPort varSerialPort)
    {
        bool _select = false;
        bool _intro = false;
        string handshakeId;
        int numOpts = Enum.GetNames(typeof(Handshake)).Length;
        string[] stopOpts = new string[numOpts];
        int id = 0;
        foreach (string handshake in Enum.GetNames(typeof(Handshake)))
        {
            stopOpts[id] = handshake;
            id++;
        }
        while (!_select)
        {
            if (!_intro)
            {
                Console.WriteLine("Available handshake options:");
                for (int i = 0; i < numOpts; i++)
                {
                    Console.Write("({0})\t", i);
                    Console.WriteLine("{0}", stopOpts[i]);
                }
                _intro = true;
                Console.WriteLine();
            }
            Console.Write("Select handshake option: ");
            handshakeId = Console.ReadLine();
            if (handshakeId == "")
            {
                _select = true;
            }
            else
            {
                if (int.Parse(handshakeId) < numOpts)
                {
                    _select = true;
                }
                if (!_select)
                {
                    Console.WriteLine(handshakeId + " is not a valid handshake option.");
                }
                else
                {
                    varSerialPort.Handshake = (Handshake)Enum.Parse(typeof(Handshake), stopOpts[int.Parse(handshakeId)], true);
                }
            }
            Console.WriteLine();
        }
    }

    // RETNINGSSTYRING


    public static double IRFeedback()
    {
        double bestValue = -1;
        double nextBestValue = -1;
        double Error;
        int phiRef = 90;
        int bestLED = -1;
        int nextBestLED = -1;
        double LEDOnRatio = -1;
        int degreeOut = -1;
        int[] LEDDegree = { 15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165 };
        int[] LEDLeftDegree = { 7, 22, 37, 52, 67, 82, 97, 112, 127, 142, 157 };
        int[] LEDRightDegree = { 22, 37, 52, 67, 82, 97, 112, 127, 142, 157, 172 };
        double offset = 8.2; //[Duty cycle %]
        double U;
        double PWMinput; // [Duty cycle %]
        double Kp = 0.05; //Proportional gain

        // Update the LED Values:
        /*for (int i = 0; i < 11; i++)
        {
            sensorAngleFollow[i] = analogRead(LEDPin[i]);
        };*/

        // Which LED's is on:
        for (int i = 0; i < numLED; i++)
        {
            if (sensorAngleFollow[i] > 5)
            {
                if (sensorAngleFollow[i] > bestValue)
                {
                    nextBestValue = bestValue;
                    nextBestLED = bestLED;
                    bestValue = sensorAngleFollow[i];
                    bestLED = i;
                }
                else
                {
                    if (sensorAngleFollow[i] > nextBestValue)
                    {
                        nextBestValue = sensorAngleFollow[i];
                        nextBestLED = i;
                    }
                }
            }
        };

        /*if ((LEDOnRatio < 2) && (LEDOnRatio > 0))
        {
            if ((bestValue == nextBestValue) && (bestValue != 0))
            {
                //Serial.print("THEY ARE EQUAL!!!!! =   ");
            }

            if ((bestLED > nextBestLED) && (bestLED != nextBestLED))
            {
                degreeOut = LEDLeftDegree[bestLED];
            }
            if ((bestLED < nextBestLED) && (bestLED != nextBestLED))
            {
                degreeOut = LEDRightDegree[bestLED];
            }
            else
            {
                degreeOut = LEDDegree[bestLED];
            }
        }
        else
        {
            degreeOut = LEDDegree[bestLED];
        }
        if (bestValue == -1)
        {
            degreeOut = 90;
        }

        Error = phiRef - degreeOut; //Udregningen af error, udefra forskellen mellem Vinkel referencen og vinkel til førerbilen

        U = Error * Kp + offset; //Inputtet til servomotoren

        if (U < 6)
        {
            U = 6;
        }
        if (U > 10)
        {
            U = 10;
        }

        PWMinput = 10.23 * U; //Omregning til et PWM signal

        return PWMinput;*/
        return 1.3;
    }






}