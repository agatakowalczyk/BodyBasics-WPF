namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Windows.Shapes;
    using System.Windows.Threading;
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {


        DispatcherTimer gameTimer = new DispatcherTimer(); // create a new instance of the dispatcher time called gameTimer

        List<Ellipse> removeThis = new List<Ellipse>(); // make a list of ellipse called remove this it will be used to remove the circles we click on from the game

        // below are all the necessary integers declared for this game
        int spawnRate = 60; // this is the default spawn rate of the circles
        int currentRate; // current rate will help add an interval between spawning of the circles
        int lastScore = 0; // this will hold the last played score for this game
        int health = 400; // total health of the player in the begining of the game
        int posX; // x position of the circles
        int posY; // y position of the circles
        int score = 0; // current score for the game

        double growthRate = 0.6; // the default growth rate for each circle in the game

        Random rand = new Random(); // a random number generator

        // below are the two media player classes one for the clicked sound and one for the pop sound

        MediaPlayer playClickSound = new MediaPlayer();
        MediaPlayer playerPopSound = new MediaPlayer();

        // below are the two URI location finder for both mp3 files we imported for this game

        Uri ClickedSound;
        Uri PoppedSound;

        // colour for the circles
        Brush brush;
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        //private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {

            // inside the main constructor we will write the instructors for the begining of the game
            gameTimer.Tick += GameLoop; // set the game timer event called game loop
            gameTimer.Interval = TimeSpan.FromMilliseconds(20); // this time will tick every 20 milliseconds
            gameTimer.Start(); // start the timer 

            currentRate = spawnRate; // set the current rate to the spawn rate number

            // locate both of the mp3 files inside sound folder and add them to the correct URI below

            ClickedSound = new Uri("pack://siteoforigin:,,,/sound/clickedpop.mp3");
            PoppedSound = new Uri("pack://siteoforigin:,,,/sound/pop.mp3");

            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

 
            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {


                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {



                //sprawdz gdzie jest babelek
                //spraawdx gdzie jest dlon (10,20)
                //jesli sa obk siebie to peknij 
                ///  jescji nie to inc nie 



                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }


        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void GameLoop(object sender, EventArgs e)
        {

            // this is the game loop event, all of the instructions inside of this event will run each time the timer ticks

            // first we update the score and show the last score on the labels
            txtScore.Content = "Score: " + score;
            txtLastScore.Content = "Last Score: " + lastScore;

            // reduce 2 from the current rate as the time runs
            currentRate -= 2;

            // if the current rate is below 1 
            if (currentRate < 1)
            {
                // reset current rate back to spawn rate
                currentRate = spawnRate;

                // generate a random number for the X and Y value for the circles
                posX = rand.Next(0, 1600);
                posY = rand.Next(0, 800);

                // generate a random colour for the circles and save it inside the brush
                //brush = new SolidColorBrush(Color.FromRgb((byte)rand.Next(1, 255), (byte)rand.Next(1, 255), (byte)rand.Next(1, 255)));
                SolidColorBrush brush = new SolidColorBrush();
                brush.Color = Colors.Pink;

                // create a new ellipse called circle
                // this circle will have a tag, default height and width, border colour and fill
                Ellipse circle = new Ellipse
                {

                    Tag = "circle",
                    Height = 50,
                    Width = 50,
                    Stroke = Brushes.MediumPurple,
                    StrokeThickness = 1,
                    Fill = brush

                };

                // place the newly created circle to the canvas with the X and Y position generated earlier
                Canvas.SetLeft(circle, posX);
                Canvas.SetTop(circle, posY);
                // finally add the circle to the canvas
                MyCanvas.Children.Add(circle);
            }

            // the for each loop below will find each ellipse inside of the canvas and grow it  

            foreach (var x in MyCanvas.Children.OfType<Ellipse>())
            {
                // we search the canvas and find the ellipse that exists inside of it

                x.Height += growthRate; // grow the height of the circle
                x.Width += growthRate; // grow the width of the circle
                x.RenderTransformOrigin = new Point(0.5, 0.5); // grow from the centre of the circle by resetting the transform origin

                // if the width of the circle goes above 70 we want to pop the circle

                if (x.Width > 120)
                {
                    // if the width if above 70 then add this circle to the remove this list
                    removeThis.Add(x);
                    health -= 30; // reduce health by 15 
                    playerPopSound.Open(PoppedSound); // load the popped sound uri inside of the player pop sound media player
                    playerPopSound.Play(); // now play the pop sound

                }
            } // end of for each loop

            // if health is above 1 
            if (health > 1)
            {
                // link the health bar rectangle to the health integer
                healthBar.Height = health;
            }
            else
            {
                // if health is below 1 then run the game over function
                GameOverFunction();
            }

            // to remov ethe ellipse from the game we need another for each loop
            foreach (Ellipse i in removeThis)
            {
                // this for each loop will search for each ellipse that exist inside of the remove this list
                MyCanvas.Children.Remove(i); // when it finds one it will remove it from the canvas
            }

            // if the score if above 5 
            if (score > 5)
            {
                // speed up the spawn rate
                spawnRate = 25;
            }

            // if the score is above 20 
            if (score > 20)
            {
                // speed up the growth and and spawn rate
                spawnRate = 15;
                growthRate = 1.5;
            }

        }

        private void GameOverFunction()
        {

            // this is the game over function 

            gameTimer.Stop(); // first stop the game timer

            // show a message box to the end screen and wait for the player to click ok
            MessageBox.Show("Game Over" + Environment.NewLine + "You Scored: " + score + Environment.NewLine + "Click Ok to play again!", "Finish: ");

            // after the player clicked ok now we need to do a for each loop
            foreach (var y in MyCanvas.Children.OfType<Ellipse>())
            {
                // find all of the existing ellipse that are on the screen and add them to the remove this list
                removeThis.Add(y);
            }
            // here we need another for each loop to remove everything from inside of the remove this list
            foreach (Ellipse i in removeThis)
            {
                MyCanvas.Children.Remove(i);
            }

            // reset all of the game values to default including clearling all of the ellipses from the remove this list
            growthRate = .6;
            spawnRate = 60;
            lastScore = score;
            score = 0;
            currentRate = 5;
            health = 350;
            removeThis.Clear();
            gameTimer.Start();


        }




    }
}
