#include <SoftwareSerial.h>
#include <TimerThree.h>
#include <VNH3SP30.h>
#include <Wire.h> // Tutorial that explaines ALL: https://www.youtube.com/watch?v=pR4iFQtYvfM&ab_channel=learnelectronics

VNH3SP30 Motor1;

const unsigned long baudRate = 57600;
const unsigned int arrayLength = 10;

///////////////////////////////////////
// WHAT TO DO IF CONNECTION IS LOST? //
///////////////////////////////////////
bool awaitingReponse = false;
bool defaultSpeed = 0;

/////////////////////////////////
// XBEE SETTINGS AND VARIABLES //
/////////////////////////////////
SoftwareSerial xbee(12, 13); // RX, TX
char xbeeChar;
bool xbeeRead = false;
bool actRead = false;
char intToArray[arrayLength];

//////////
// TIME //
//////////
unsigned long currentTime = 0;
unsigned long previousTime = 0;
const unsigned int sampleInterval = 65;

//////////////////////////
// INFRARED LED ARRAY   //
//////////////////////////
const char sensorAngleFollowTag = 'L';
const unsigned int numLED = 11;
unsigned int sensorAngleFollow[numLED] = {0, 0, 0, 0, 50, 1000, 50, 0, 0, 0, 0};
// Pins
const int pinLED[numLED] = {A10, A9, A8, A7, A6, A5, A4, A3, A2, A1, A0};
const int pinServo = 2;

////////////////
// TACHOMETER //
////////////////
const char sensorSpeedFollowTag = 'M';
int sensorSpeedFollow[8] = {0, 0, 0, 0, 0, 0, 0, 0};
// Pins
const int byteRead = 20;
const int pinBit[8] = {33, 35, 37, 39, 41, 43, 45, 47};

//////////////////////////
// Motor pins Mega H-bro
//////////////////////////
#define M1_PWM 6    // pwm pin motor
#define M1_INA 4    // control pin INA
#define M1_INB 5    // control pin INB
#define M1_DIAG 3   // diagnose pins (combined DIAGA/ENA and DIAGB/ENB)
#define M1_CS A15    // current sense pin

////////////////////////////////
// Motor Bib funktion
////////////////////////////////
void receiveEvent(int howMany) {
  while (1 < Wire.available()) { // loop through all but the last
    char c = Wire.read(); // receive byte as character
    Serial.print("Char = ");
    Serial.println(c);  // print the received messege
  }
  int x = Wire.read(); // receive byte as an integer
  Serial.print("Int = ");
  Serial.println(x);
}

///////////////
// ACTUATORS //
///////////////

struct Actuator {
  //bool    writable = false;
  char    input[arrayLength];
  int     counter = 0;
  double  output = 0.0;
} A, B, *actSelect;

void setup() {
  // Pins
  for (int i = 0; i < numLED; i++) {
    pinMode(pinLED[i], INPUT);
  }
  pinMode(pinServo, OUTPUT);
  pinMode(byteRead, INPUT);
  for (int i = 0; i < 8; i++) {
    pinMode(pinBit[i], INPUT);
  }

  // Interrupt
  attachInterrupt(digitalPinToInterrupt(byteRead), tachoRead, RISING);

  // PWM timer
  Timer3.initialize(2000);

  // H-bro setup
  Motor1.begin(M1_PWM, M1_INA, M1_INB, M1_DIAG, M1_CS);    // Motor 1 object connected through specified pins
  Wire.begin(8);  //join i2c bus with #8
  Wire.onReceive(receiveEvent); //register event

  // PWM Frekvens Timer 4 pin (6, 7, 8)
  int myEraser = 7;      // this is 111 in binary and is used as an eraser
  TCCR4B &= ~myEraser;   // this operation (AND plus NOT),  set the three bits in TCCR2B to 0

  int myPrescaler = 1;   // this could be a number in [1 , 6]. In this case, 3 corresponds in binary to 011. Svarer til 31kHz
  TCCR4B |= myPrescaler; //this operation (OR), replaces the last three bits in TCCR2B with our new value 011

  // Serial
  Serial.begin(baudRate);
  while (!Serial) {
    ; // wait for serial port to connect.
  }
  Serial.println("Serial loaded");
  xbee.begin(baudRate);
  Serial.println("XBee loaded");
  xbee.println("XBee loaded");
}

void loop() {
  xbeeChar = '\0';

  ////////////////////////////////////////
  // READ SENSOR DATA AND WRITE TO XBEE //
  ////////////////////////////////////////
  currentTime = millis();
  if ((currentTime - previousTime) > sampleInterval) {
    /////////////////////////////////////////
    // NO RESPONSE? THEN USE DEFAULT SPEED //
    /////////////////////////////////////////
    if (awaitingReponse == true) {
      B.output = B.output * 0.9;
      Motor1.setSpeed(B.output);
    }

    //Serial.println("Sample begin");

    xbee.write("?");

    //////////////////////////
    // INFRARED LED ARRAY   //
    //////////////////////////
    xbee.write(sensorAngleFollowTag);
    for (int i = 0; i < numLED; i++) {
      if (!(i == 0)) {
        xbee.write("/");
      }
      /*sensorAngleFollow[i] = analogRead(A15);*/
      sensorAngleFollow[i] = analogRead(pinLED[i]);
      itoa(sensorAngleFollow[i], intToArray, arrayLength);
      xbee.write(intToArray);
    }
    xbee.write("&");

    ////////////////
    // TACHOMETER //
    ////////////////
    xbee.write(sensorSpeedFollowTag);
    //sensorSpeedFollow = analogRead(A15);
    /*itoa(sensorSpeedFollow, intToArray, arrayLength);
      xbee.write(intToArray);*/
    noInterrupts();
    for (int i = 0; i < 8; i++) {
      //char speedyGonzalez = sensorSpeedFollow[i];
      //xbee.write(sensorSpeedFollow[i]);
      //xbee.write(speedyGonzalez);
      if (sensorSpeedFollow[i]) {
        xbee.write("1");
      }
      else {
        xbee.write("0");
      }
    }
    interrupts();
    xbee.write("&");

    //////////
    // TIME //
    //////////
    xbee.write("T");
    itoa(currentTime, intToArray, arrayLength);
    xbee.write(intToArray);
    xbee.write("&");

    ////////////
    // FINISH //
    ////////////
    xbee.write("!\n");
    previousTime = currentTime;

    /////////////////////////////////////////////
    // NOTE THAT WE ARE WAITING FOR A RESPONSE //
    /////////////////////////////////////////////
    awaitingReponse = true;
  }

  ////////////////////
  // READ FROM XBEE //
  ////////////////////
  if (xbee.available()) {
    xbeeChar = xbee.read();
    //Serial.println(xbeeChar);

    /////////////////////////////
    // '?' STARTS THE SEQUENCE //
    /////////////////////////////
    if (xbeeChar == '?') {
      xbeeRead = true;
      actRead = false;
      //Serial.println("Read begin");
    }
    ////////////////////////////////////////////////////////////
    // IF SEQUENCE HAS STARTED, INTERPRET FOLLOWING CHARACTER //
    ////////////////////////////////////////////////////////////
    else if (xbeeRead) {
      /////////////////////
      // '!' FINISHES IT //
      /////////////////////
      if (xbeeChar == '!') {
        xbeeRead = false;
        actRead = false;
        Timer3.pwm(pinServo, A.output, 20000);
        //Serial.println("Read end");
        /*Serial.println("Angle\tSpeed");
          Serial.print(A.output);
          Serial.print("\t");
          Serial.println(B.output);*/
        Motor1.setSpeed(B.output);

        /////////////////////////////
        // A RESPONSE WAS RECEIVED //
        /////////////////////////////
        awaitingReponse = false;
      }
      ////////////////////////////////////////////////////////
      // IF ACTUATOR IS SELECTED, SEARCH FOR BREAK OR VALUE //
      ////////////////////////////////////////////////////////
      else {
        if (actRead) {
          if (xbeeChar == '&') {
            actRead = false;
            actSelect->input[actSelect->counter] = '\0'; // Insert NULL character to end string.
            actSelect->output = atof(actSelect->input); // Convert string to a double.
            //Serial.println(actSelect->output);
          }
          else {
            actSelect->input[actSelect->counter] = xbeeChar;
            actSelect->counter++;
          }
        }
        ////////////////////////////////////////////
        // IF NO ACTUATOR IS SELECTED, SELECT ONE //
        ////////////////////////////////////////////
        else {
          actRead = true;
          switch (xbeeChar) {
            case 'A':
              actSelect = &A;
              //Serial.println("Angle begin");
              break;
            case 'B':
              actSelect = &B;
              //Serial.println("Speed begin");
              break;
            default:
              actRead = false;
              break;
          }
          if (actRead) {
            actSelect->counter = 0;
          }
        }
      }
    }
  }
}

void tachoRead() {
  //Serial.println("Tacho begin");
  for (int i = 0; i < 8; i++) {
    sensorSpeedFollow[i] = digitalRead(pinBit[i]);
    Serial.print(sensorSpeedFollow[i]);
  }
  Serial.println();
}
