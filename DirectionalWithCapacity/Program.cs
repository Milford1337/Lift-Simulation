// --- The program simulates a lift that can accommodate multiple passengers at once, and which has a capacity.
// --- Broadly speaking, the lift serves all calls in one direction (all internal, and external when it has capacity).
// --- When there are no more internal calls, or external calls which the lift has capacity for, in its current direction, it changes direction.

// --- The program takes exactly one input, a string input corresponding to the csv file.
// --- The program outputs to the console the total time taken for the lift to serve all passengers.

// --- The program also creates a csv file that details all logged events in the simulation.
// --- The following events are logged: a lift state change; the lift arriving at a floor; a passenger collection completion; and a passenger drop off completion.
// --- Each log entry states: the time of the event, the floor of the lift at that time, the nature of the event, the direction of the lift,
// --- and a list of the IDs of passengers in the lift.

// --- The program assumes the lift: (i) starts at floor 1; (ii) takes 10 seconds to move between floors;
// --- (iii) takes 5 seconds to collect a passenger; (iv) takes 5 seconds to drop a passenger; and (v) has a capacity of 8.
// --- Each of (i)-(v) can be adjusted by passing custom int values into the Lift constructor when the Lift object is created in Main().


using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;





namespace DirectionalAlgorithmWithCapacity

{
    // Define a Passenger class. An instance will be created for each row of the input csv file.
    // The class includes a property for each column of the csv file.

    public class Passenger
    {
        public int Id { get; set; }
        public int StartFloor { get; set; }
        public int DestFloor { get; set; }
        public int StartTime { get; set; }
    }


    // In order to map the csv file columns to Passenger properties, we define the PassengerMap class.
    // Details:
    // PassengerMap implements ClassMap<Passenger> of the generic ClassMap<T> class from the CsvHelper.Configuration namespace.
    // This is a non-static class with a constructor that calls Map() on the PassengerMap instance.
    // A PassengerMap instance stores the mappings of csv columns names to Passenger properties.
    // Later, when we associate the overall mapping with a CsvReader object, we will generate a PassengerMap instance under the hood.


    public class PassengerMap : ClassMap<Passenger>
    {
        public PassengerMap()
        {
            this.Map(p => p.Id).Name("Person ID");
            this.Map(p => p.StartFloor).Name("At Floor");
            this.Map(p => p.DestFloor).Name("Going to Floor");
            this.Map(p => p.StartTime).Name("Time");
        }
    }


    // Define a LogEntry class. 
    // Before the simulation, we will create a List<LogEntry> object.
    // During the lift simulation, a LogEntry instance will be created and added to the List<LogEntry> every time something needs to be logged.
    // After the simulation, we will use the List<LogEntry> to write to a csv file.
    // LogEntry class property names have been chosen to avoid the need for a mapper class implementing ClassMap<LogEntry>.

    public class LogEntry
    {
        public int Time { get; set; }
        public int Floor { get; set; }
        public string Event { get; set; }
        public string LiftDirection { get; set; }
        public string PassengersInLift { get; set; } = "";

        public LogEntry(int time, int floor, string eventstring, string liftdirection, string passengersinlift)
        {
            this.Time = time;
            this.Floor = floor;
            this.Event = eventstring;
            this.LiftDirection = liftdirection;
            this.PassengersInLift = passengersinlift;
        }

    }





    // Define a Lift class.

    public class Lift
    {
        public int CurrentFloor { get; set; }
        public LiftState State { get; set; }
        public List<Passenger> PassengerCalls { get; set; }
        public List<Passenger> PassengersInLift { get; set; }
        public int TimeBetweenFloors { get; set; }
        public int CollectionTime { get; set; }
        public int DropTime { get; set; }
        public int Capacity { get; set; }
        public int? TimeToCollectionEnd { get; set; }
        public int? TimeToDropEnd { get; set; }
        public int? TimeToNextFloor { get; set; }

        public Passenger? DroppingPassenger { get; set; }
        public Passenger? CollectingPassenger { get; set; }
        public LiftDirection Direction { get; set; }

        public Lift(int currentfloor = 1, int timebetweenfloors = 10, int collectiontime = 5, int droptime = 5, int capacity = 8)
        {
            this.CurrentFloor = currentfloor;
            this.State = LiftState.Idle;
            this.PassengerCalls = new List<Passenger>();
            this.PassengersInLift = new List<Passenger>();
            this.TimeBetweenFloors = timebetweenfloors;
            this.CollectionTime = collectiontime;
            this.DropTime = droptime;
            this.Capacity = capacity;
            this.TimeToCollectionEnd = null;
            this.TimeToDropEnd = null;
            this.TimeToNextFloor = null;
            this.DroppingPassenger = null;
            this.CollectingPassenger = null;
            this.Direction = LiftDirection.None;
        }

    }

    // Define a corresponding LiftState enum.

    public enum LiftState
    {
        Idle,
        MovingUp,
        MovingDown,
        Collecting,
        Dropping
    }

    // Define a corresponding LiftDirection enum

    public enum LiftDirection
    {
        None,
        Up,
        Down
    }



    class Program
    {
        static void Main(string[] args)
        {

            // --- Handle the input string ---

            // Check that exactly one string input was received.

            if (args.Length != 1)
            {
                Console.WriteLine("Input exactly one string: the csv filename");
                return;
            }

            Console.WriteLine($"Received exactly one string input: {args[0]}");

            string inputFileString = args[0];

            // Check that inputFileString corresponds to a csv file.

            if (!File.Exists(inputFileString))
            {
                Console.WriteLine("The string does not correspond to a file");
                return;
            }

            if (Path.GetExtension(inputFileString).ToLower() != ".csv")
            {
                Console.WriteLine("That file does not have a csv extension");
                return;
            }

            Console.WriteLine("Input csv file successfully selected");





            // --- Generate a Passenger<List> from the input csv file ---

            // Introduce a List<Passenger> variable, passengers, and assign it to an empty List<Passenger>.

            List<Passenger> passengers = new List<Passenger>();


            // Access our csv file in order to populate passengers with Passenger instances.
            // As a precaution, wrap this in a try...catch block. The main program ends if an error is thrown.

            try
            {
                // Create a CsvReader instance, csv, with a using block.

                using (StreamReader sr = new StreamReader(inputFileString))
                using (CsvReader csv = new CsvReader(sr, CultureInfo.InvariantCulture))
                {

                    // Assign our mapping to csv.
                    // Details:
                    // Call the RegisterClassMap<PassengerMap>() method on the CsvContext object corresponding to csv.
                    // Under the hood, this generates a PassengerMap instance and passes it as an argument into the RegisterClassMap() method call on the CsvContext object.

                    csv.Context.RegisterClassMap<PassengerMap>();

                    // Populate passengers with Passenger objects.
                    // Details:
                    // (i) call the GetRecords<Passenger>() method on csv, which returns an IEnumerable<Passenger>;
                    // (ii) call ToList() on the IEnumerable<Passenger>.

                    passengers = csv.GetRecords<Passenger>().ToList();

                    Console.WriteLine("Input csv file successfully read");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read the input file: {e.Message}");
                return;
            }


            // Check that there are no troll Passenger objects.
            // A Passenger object p is a troll iff p.StartFloor == p.DestFloor.

            foreach (Passenger p in passengers)
            {
                if (p.StartFloor == p.DestFloor)
                {
                    Console.WriteLine($"Troll found: passenger {p.Id} has the same start and destination floors.");
                    return;
                }
            }
            Console.WriteLine("No troll passengers");






            // Create the log, a List<LogEntry> object.

            List<LogEntry> log = new List<LogEntry>();


            // Create the lift.

            Lift lift = new Lift();


            // Set currentTime to 0.

            int currentTime = 0;

            // Create the list of served passengers, a List<Passenger> object.

            List<Passenger> servedPassengers = new List<Passenger>();


            // Create a method for simply adding entries to log.

            void AddLog(string eventDescription)
            {
                string stringdirection = lift.Direction.ToString();

                var liftPassengerIds = from p in lift.PassengersInLift
                                       select p.Id;
                string stringpassengersinlift = String.Join(",", liftPassengerIds);

                LogEntry newEntry = new LogEntry(currentTime, lift.CurrentFloor, eventDescription, stringdirection, stringpassengersinlift);
                log.Add(newEntry);
            }


            // Create methods to ease passing arguments into Addlog().

            string StateChange()
            {
                return $"Lift updated state to {lift.State}";
            }

            string FloorArrival()
            {
                return $"Lift arrived at floor {lift.CurrentFloor}";
            }
            string PassengerCollection()
            {
                return $"Passenger ID {lift.CollectingPassenger!.Id} collection completed";
            }
            string PassengerDrop()
            {
                return $"Passenger ID {lift.DroppingPassenger!.Id} drop off completed";
            }




            // --- Simulation ---

            // Define the main simulation loop.

            while (servedPassengers.Count < passengers.Count && currentTime < 10000)        // Added the second conjunct for safety!
            {


                // Increment the time.

                currentTime += 1;



                // Update the passenger calls.

                var newCalls = from p in passengers
                               where p.StartTime == currentTime
                               select p;

                lift.PassengerCalls.AddRange(newCalls.ToList());


                // Capture lift behaviour.


                // === Check if the lift is in the Idle state ===

                // If it is, check if PassengerCalls is empty. There are two cases:

                // (1) PassengerCalls is empty.
                // This represents that the lift has completed all previous passenger calls, and no new calls occurred at currentTime.
                // Skip the rest of the loop (this captures the protocol to remain stationary when idle).

                // (2) PassengerCalls is non-empty.
                // This occurs when the lift has served all previous calls, and one or more calls appear at currentTime.
                // The lift selects the closest new Passenger. In the case of ties, it selects the Passenger with the lowest Id.
                // There are three cases:

                // (2.A)
                // The DestFloor of the Passenger is the CurrentFloor of the lift.
                // The Direction of the lift remains None (our logic guarantees the lift has the None Direction in the Idle state).
                // The lift moves into the Collecting state.
                // ...

                // (2.B) The DestFloor of the Passenger is above the CurrentFloor of the lift.
                // The lift sets its Direction to Up, and moves into the MovingUp state.
                // ...

                // (2.C) The DestFloor of the Passenger is below the CurrentFloor of the lift.
                // The lift sets its Direction to Down, and moves into the MovingDown state.
                // ...


                if (lift.State == LiftState.Idle)
                {
                    if (lift.PassengerCalls.Count == 0)                         // (1)
                    {
                        continue;
                    }
                    else                                                        // (2)
                    {
                        Passenger targetPassenger = (from p in lift.PassengerCalls
                                                     orderby p.StartTime, p.Id
                                                     select p).First();
                        int targetFloor = targetPassenger.StartFloor;

                        if (lift.CurrentFloor == targetFloor)                   // (2.A)
                        {
                            lift.CollectingPassenger = targetPassenger;
                            lift.State = LiftState.Collecting;
                            AddLog(StateChange());
                            lift.TimeToCollectionEnd = lift.CollectionTime;
                            lift.TimeToCollectionEnd -= 1;
                            continue;
                        }
                        else if (lift.CurrentFloor < targetFloor)               // (2.B)
                        {
                            lift.Direction = LiftDirection.Up;
                            lift.State = LiftState.MovingUp;
                            AddLog(StateChange());
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
                        }
                        else                                                    // (2.C)
                        {
                            lift.Direction = LiftDirection.Down;
                            lift.State = LiftState.MovingDown;
                            AddLog(StateChange());
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
                        }
                    }
                }


                // === Check if the lift is in the Collecting state ===

                // If it is, we check whether TimeToCollectionEnd is 0.
                // There are two cases:

                // (1) TimeToCollectionEnd is not 0.
                // We decrement TimeToCollectionEnd by 1 and skip the rest of the loop.

                // (2) TimeToCollectionEnd is 0.
                // Collection is completed.
                // We now check whether both there are additional Passengers to collect at the CurrentFloor and the lift has capacity.
                // There are two cases:

                // (2.A) There are additional Passengers to collect at the CurrentFloor and the lift has capacity.
                // The lift remains in the Collecting state.
                // ...

                // (2.B) Either there are no additional Passengers to collect at the CurrentFloor or the lift is at capacity.
                // We check the Direction of the lift. There are three cases.

                // (2.B.i) The lift has the None Direction.
                // Select a Direction based on the DestFloor of the internal Passenger with the lowest StartTime.
                // If the Direction selected is Up, the lift moves into the MovingUp state.
                // ...
                // If the Direction selected is Down, the lift moves into the MovingDown state.
                // ...

                // (2.B.ii) The lift has the Up Direction.
                // We first check if there is either (a) an internal call from a floor above the CurrentFloor or
                // (b) an external call from a floor above the CurrentFloor and the lift has capacity for it.
                // There are two cases:

                // (2.B.ii.i) There is such a call.
                // The lift moves into the MovingUp state.
                // ...

                // (2.B.ii.ii) There are no such calls.
                // The lift changes Direction to Down.
                // The lift moves into the MovingDown state.
                // ...


                // (2.B.iii) The lift has the Down Direction.
                // We first check if there is either (a) an internal call from a floor below the CurrentFloor or
                // (b) an external call from a floor below the CurrentFloor and the lift has capacity for it.
                // There are two cases:

                // (2.B.iii.i) There is such a call.
                // The lift moves into the MovingDown state.
                // ...

                // (2.B.iii.ii) There are no such calls.
                // The lift changes Direction to Up.
                // The lift moves into the MovingUp state.
                // ...


                if (lift.State == LiftState.Collecting)
                {
                    if (lift.TimeToCollectionEnd != 0)                                      // (1)
                    {
                        lift.TimeToCollectionEnd -= 1;
                        continue;
                    }
                    else                                                                    // (2)                                              
                    {
                        lift.PassengersInLift.Add(lift.CollectingPassenger!);
                        AddLog(PassengerCollection());
                        lift.CollectingPassenger = null;
                        lift.TimeToCollectionEnd = null;

                        var toCollect = (from p in lift.PassengerCalls
                                         where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                         select p).ToList();
                        if (toCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)        // (2.A)
                        {
                            lift.CollectingPassenger = (from p in toCollect
                                                        orderby p.StartTime, p.Id
                                                        select p).First();
                            lift.State = LiftState.Collecting;
                            AddLog(StateChange());
                            lift.TimeToCollectionEnd = lift.CollectionTime;
                            lift.TimeToCollectionEnd -= 1;
                            continue;
                        }

                        else                                                                            // (2.B)
                        {
                            if (lift.Direction == LiftDirection.None)                                   // (2.B.i)
                            {
                                Passenger targetPassenger = (from p in lift.PassengersInLift
                                                             orderby p.StartTime, p.Id
                                                             select p).First();
                                int targetFloor = targetPassenger.DestFloor;

                                if (targetFloor > lift.CurrentFloor)
                                {
                                    lift.Direction = LiftDirection.Up;
                                    lift.State = LiftState.MovingUp;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                                else
                                {
                                    lift.Direction = LiftDirection.Down;
                                    lift.State = LiftState.MovingDown;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }

                            }
                            else if (lift.Direction == LiftDirection.Up)                                // (2.B.ii)
                            {
                                List<Passenger> internalCalls = (from p in lift.PassengersInLift
                                                                 where p.DestFloor > lift.CurrentFloor
                                                                 select p).ToList();
                                List<Passenger> externalCalls = (from p in lift.PassengerCalls
                                                                 where !lift.PassengersInLift.Contains(p) && p.StartFloor > lift.CurrentFloor && lift.PassengersInLift.Count < lift.Capacity
                                                                 select p).ToList();
                                if (internalCalls.Count != 0 || externalCalls.Count != 0)               // (2.B.ii.i)
                                {
                                    lift.State = LiftState.MovingUp;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                                else                                                                    // (2.B.ii.ii)
                                {
                                    lift.State = LiftState.MovingDown;
                                    lift.Direction = LiftDirection.Down;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }

                            }
                            else                                                                        // (2.B.iii)
                            {
                                List<Passenger> internalCalls = (from p in lift.PassengersInLift
                                                                 where p.DestFloor < lift.CurrentFloor
                                                                 select p).ToList();
                                List<Passenger> externalCalls = (from p in lift.PassengerCalls
                                                                 where !lift.PassengersInLift.Contains(p) && p.StartFloor < lift.CurrentFloor && lift.PassengersInLift.Count < lift.Capacity
                                                                 select p).ToList();
                                if (internalCalls.Count != 0 || externalCalls.Count != 0)               // (2.B.iii.i)
                                {
                                    lift.State = LiftState.MovingDown;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                                else                                                                    // (2.B.iii.ii)
                                {
                                    lift.Direction = LiftDirection.Up;
                                    lift.State = LiftState.MovingUp;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                            }
                        }
                    }
                }









                // === Check if the lift is in the Dropping state ===

                // If it is, we check whether TimeToDropEnd is 0.
                // There are two cases:

                // (1) TimeToDropEnd is not 0.
                // We decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2) TimeToDropEnd is 0.
                // Dropping completed.
                // We first check whether PassengerCalls is empty.

                // (2.i) PassengerCalls is empty.
                // The lift moves into the Idle state and updates its Direction to None.
                // Skip the rest of the loop (this implements the protocol to remain stationary when idle).

                // (2.ii) PassengerCalls is non-empty.
                // We check whether there are additional Passengers to drop at the CurrentFloor.

                // (2.ii.A) There are additional Passengers to drop at the CurrentFloor.
                // The lift remains in the Dropping state.
                // ...

                // (2.ii.B) There are no additional Passengers to drop at the CurrentFloor.
                // We check whether both there are Passengers to collect at the CurrentFloor and the lift has capacity.
                // There are two cases:

                // (2.ii.B.i) There are Passengers to collect at the CurrentFloor and the lift has capacity.
                // The lift moves into the Collecting state.
                // ...

                // (2.ii.B.ii) Either there are no Passengers to collect at the CurrentFloor or the lift is at capacity.
                // We check the Direction of the lift. There are two cases.

                // (2.ii.B.ii.i) The lift has the Up Direction.
                // We first check if there is either (a) an internal call from a floor above the CurrentFloor or
                // (b) an external call from a floor above the CurrentFloor and the lift has capacity for it.
                // There are two cases:

                // (2.ii.B.ii.i.A) There is such a call.
                // The lift moves into the MovingUp state.
                // ...

                // (2.ii.B.ii.i.B) There are no such calls.
                // The lift changes Direction to Down.
                // The lift moves into the MovingDown state.
                // ...


                // (2.ii.B.ii.ii) The lift has the Down Direction.
                // We first check if there is either (a) an internal call from a floor below the CurrentFloor or
                // (b) an external call from a floor below the CurrentFloor and the lift has capacity for it.
                // There are two cases:

                // (2.ii.B.ii.ii.A) There is such a call.
                // The lift moves into the MovingDown state.
                // ...

                // (2.ii.B.ii.ii.B) There are no such calls.
                // The lift changes Direction to Up.
                // The lift moves into the MovingUp state.
                // ...


                if (lift.State == LiftState.Dropping)
                {
                    if (lift.TimeToDropEnd != 0)                                    // (1)
                    {
                        lift.TimeToDropEnd -= 1;
                        continue;
                    }
                    else                                                            // (2)
                    {
                        servedPassengers.Add(lift.DroppingPassenger!);
                        lift.PassengerCalls.Remove(lift.DroppingPassenger!);
                        lift.PassengersInLift.Remove(lift.DroppingPassenger!);
                        AddLog(PassengerDrop());
                        lift.DroppingPassenger = null;
                        lift.TimeToDropEnd = null;

                        if (lift.PassengerCalls.Count == 0)                         // (2.i)
                        {
                            lift.State = LiftState.Idle;
                            lift.Direction = LiftDirection.None;
                            AddLog(StateChange());
                            continue;
                        }
                        else                                                        // (2.ii)
                        {
                            var toDrop = (from p in lift.PassengersInLift
                                          where p.DestFloor == lift.CurrentFloor
                                          select p).ToList();

                            if (toDrop.Count > 0)                                   // (2.ii.A)
                            {
                                lift.DroppingPassenger = (from p in toDrop
                                                            orderby p.StartTime, p.Id
                                                            select p).First();
                                lift.State = LiftState.Dropping;
                                AddLog(StateChange());
                                lift.TimeToDropEnd = lift.DropTime;
                                lift.TimeToDropEnd -= 1;
                                continue;
                            }
                            else                                                    // (2.ii.B)
                            {
                                var toCollect = (from p in lift.PassengerCalls
                                                 where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                                 select p).ToList();
                                if (toCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)        // (2.ii.B.i)
                                {
                                    lift.CollectingPassenger = (from p in toCollect
                                                                orderby p.StartTime, p.Id
                                                                select p).First();
                                    lift.State = LiftState.Collecting;
                                    AddLog(StateChange());
                                    lift.TimeToCollectionEnd = lift.CollectionTime;
                                    lift.TimeToCollectionEnd -= 1;
                                    continue;
                                }
                                else                                                                            // (2.ii.B.ii)
                                {
                                    if (lift.Direction == LiftDirection.Up)                                     // (2.ii.B.ii.i)
                                    {
                                        List<Passenger> internalCalls = (from p in lift.PassengersInLift
                                                                         where p.DestFloor > lift.CurrentFloor
                                                                         select p).ToList();
                                        List<Passenger> externalCalls = (from p in lift.PassengerCalls
                                                                         where !lift.PassengersInLift.Contains(p) && p.StartFloor > lift.CurrentFloor && lift.PassengersInLift.Count < lift.Capacity
                                                                         select p).ToList();
                                        if (internalCalls.Count != 0 || externalCalls.Count != 0)               // (2.ii.B.ii.i.A)
                                        {
                                            lift.State = LiftState.MovingUp;
                                            AddLog(StateChange());
                                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                            lift.TimeToNextFloor -= 1;
                                            continue;
                                        }
                                        else                                                                    // (2.ii.B.ii.i.B)    
                                        {
                                            lift.Direction = LiftDirection.Down;
                                            lift.State = LiftState.MovingDown;
                                            AddLog(StateChange());
                                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                            lift.TimeToNextFloor -= 1;
                                            continue;
                                        }
                                    }
                                    else                                                                        // (2.ii.B.ii.ii)                                                      
                                    {
                                        List<Passenger> internalCalls = (from p in lift.PassengersInLift
                                                                         where p.DestFloor < lift.CurrentFloor
                                                                         select p).ToList();
                                        List<Passenger> externalCalls = (from p in lift.PassengerCalls
                                                                         where !lift.PassengersInLift.Contains(p) && p.StartFloor < lift.CurrentFloor && lift.PassengersInLift.Count < lift.Capacity
                                                                         select p).ToList();
                                        if (internalCalls.Count != 0 || externalCalls.Count != 0)               // (2.B.iii.i)
                                        {
                                            lift.State = LiftState.MovingDown;
                                            AddLog(StateChange());
                                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                            lift.TimeToNextFloor -= 1;
                                            continue;
                                        }
                                        else                                                                    // (2.B.iii.ii)
                                        {
                                            lift.Direction = LiftDirection.Up;
                                            lift.State = LiftState.MovingUp;
                                            AddLog(StateChange());
                                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                            lift.TimeToNextFloor -= 1;
                                            continue;
                                        }
                                    }
                                }
                            }
                        }

                    }
                }





                // === Check if the lift is in the MovingUp state or the MovingDown state ===

                // // If the lift is in one of the states, we check whether TimeToNextFloor is 0.
                // There are two cases.

                // (1) TimeToNextFloor is not 0.
                // This represents that the lift is not at the next floor.
                // The lift moves.
                // We decrement TimeToNextFloor by 1 and skip the rest of the loop.

                // (2) TimeToNextFloor has reached 0.
                // This represents that the lift is at the next floor. We update our CurrentFloor.
                // We first check whether there are Passengers to drop at the CurrentFloor.

                // (2.A) There are Passengers to drop at the CurrentFloor.
                // The lift moves into the Dropping state.
                // ...

                // (2.B) There are no Passengers to drop at the CurrentFloor.
                // We check whether both there are Passengers to collect at the CurrentFloor and the lift has capacity.
                // There are two cases:

                // (2.B.i) There are Passengers to collect at the CurrentFloor and the lift has capacity.
                // The lift moves into the Collecting state.
                // ...

                // (2.B.ii) Either there are no Passengers to collect at the CurrentFloor or the lift is at capacity.
                // There are two cases now.

                // (2.B.ii.i) The lift is in the MovingUp state.
                // It continues in the MovingUp state.
                // Note that there is no need to check if there is either (a) an internal call from a floor above the CurrentFloor or
                // If the lift has moved up to this floor, and there are neither internal nor external (with some capacity) call at CurrentFloor,
                // then there must be such a call above the CurrentFloor.

                // (2.B.ii.ii) The lift is in the MovingDown state.
                // It continues in the MovingDown state.
                // Again, there is no need to check for appropriate calls below the CurrentFloor. This is guaranteed by our logic.

                // As both (2.B.ii) cases involve the same logic, there is no need for a conditional block.




                if (lift.State == LiftState.MovingUp || lift.State == LiftState.MovingDown)
                {
                    if (lift.TimeToNextFloor != 0)                                  // (1)
                    {
                        lift.TimeToNextFloor -= 1;
                        continue;

                    }
                    else                                                            // (2)
                    {
                        if (lift.State == LiftState.MovingUp)
                        {
                            lift.CurrentFloor += 1;

                        }
                        else
                        {
                            lift.CurrentFloor -= 1;
                        }
                        AddLog(FloorArrival());
                        lift.TimeToNextFloor = null;

                        var toDrop = (from p in lift.PassengersInLift
                                      where p.DestFloor == lift.CurrentFloor
                                      select p).ToList();

                        if (toDrop.Count > 0)                                       // (2.A)
                        {
                            lift.DroppingPassenger = (from p in toDrop
                                                        orderby p.StartTime, p.Id
                                                        select p).First();
                            lift.State = LiftState.Dropping;
                            AddLog(StateChange());
                            lift.TimeToDropEnd = lift.DropTime;
                            lift.TimeToDropEnd -= 1;
                            continue;
                        }
                        else                                                        // (2.B)
                        {
                            var toCollect = (from p in lift.PassengerCalls
                                             where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                             select p).ToList();
                            if (toCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)        // (2.B.i)
                            {
                                lift.CollectingPassenger = (from p in toCollect
                                                            orderby p.StartTime, p.Id
                                                            select p).First();
                                lift.State = LiftState.Collecting;
                                AddLog(StateChange());
                                lift.TimeToCollectionEnd = lift.CollectionTime;
                                lift.TimeToCollectionEnd -= 1;
                                continue;
                            }
                            else                                                                            // (2.B.ii)
                            {
                                lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                lift.TimeToNextFloor -= 1;
                                continue;
                            }
                                
                            
                        }


                    }
                }





            }

             // --- Print the total time taken to the console ---

            Console.WriteLine("Simulation complete");

            Console.WriteLine($"The lift serves all calls at {currentTime} seconds");





            // --- Create csv file with log information ---

            string outputFileString = inputFileString + "_log.csv";

            try
            {
                using (StreamWriter sw = new StreamWriter(outputFileString))
                using (CsvWriter csvOutput = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    csvOutput.WriteRecords(log);
                    Console.WriteLine("Output csv file successfully created: " + outputFileString);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to create the log file: {e.Message}");
            }
        
        }
    }
}