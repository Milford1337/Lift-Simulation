// --- The program simulates a lift that can accommodate multiple passengers at once, and which has a capacity.
// --- The lift algorithm sets a target passenger with a First In priority selection protocol.
// --- However, on the way to collecting and dropping the target passenger, at each floor, the lift:
// --- collects/drops extra passengers that need to be collected/dropped at that floor.
// --- Speaking broadly: at a new floor, the priority of actions is as follows (ordered from highest to lowest priority):
// --- (1) Drop the target passenger (if they are inside the lift and their dest floor has been reached).
// --- (2) Drop extra internal passengers.
// --- (3) Collect the target passenger (if they are external and their start floor has been reached).
// --- (4) Collect extra external passengers.

// --- Capacity comes into play at two points:
// --- (A) The lift only collects extra external passengers if it has capacity.
// --- (B) If (i) the target passenger is outside the lift, (ii) the lift arrives at the target passenger's start floor and would otherwise start collecting them,
// --- and (iii) the lift is at capacity, then:
// --- the lift moves into an emergency state. In this state, the lift moves to the closest destination floor of a current internal passenger,
// --- then drops them off, then immediately returns to the target passenger's start floor, and collects the target passenger.


// --- The program takes exactly one input, a string input corresponding to the csv file.
// --- The program outputs to the console the total time taken for the lift to serve all passengers.

// --- The program also creates a csv file that details all logged events in the simulation.
// --- The following events are logged: a lift state change; the lift arriving at a floor; a passenger collection completion; and a passenger drop off completion.
// --- Each log entry states: the time of the event, the floor of the lift at that time, the nature of the event, the ID of the passenger being served (0 in the case of no one),
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




namespace FifoAlgorithmPlusWithCapacity

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
        public int TargetPassenger { get; set; }
        public string PassengersInLift { get; set; } = "";

        public LogEntry(int time, int floor, string eventstring, int passenger, string passengersinlift)
        {
            this.Time = time;
            this.Floor = floor;
            this.Event = eventstring;
            this.TargetPassenger = passenger;
            this.PassengersInLift = passengersinlift;
        }

    }

    

    

    // Define a Lift class.

    public class Lift
    {
        public int CurrentFloor { get; set; }
        public LiftState State { get; set; }
        public Passenger? TargetPassenger { get; set; }
        public List<Passenger> PassengerCalls { get; set; }
        public List<Passenger> PassengersInLift { get; set; }
        public int TimeBetweenFloors { get; set; }
        public int CollectionTime { get; set; }
        public int DropTime { get; set; }
        public int Capacity { get; set; }
        public int? TargetFloor { get; set; }
        public int? TimeToCollectionEnd { get; set; }
        public int? TimeToDropEnd { get; set; }
        public int? TimeToNextFloor { get; set; }

        public Passenger? DroppingPassenger { get; set; }
        public Passenger? CollectingPassenger { get; set; }
        public Passenger? EmergencyTarget { get; set; }

        public Lift(int currentfloor = 1, int timebetweenfloors = 10, int collectiontime = 5, int droptime = 5, int capacity = 8)
        {
            this.CurrentFloor = currentfloor;
            this.State = LiftState.Idle;
            this.TargetPassenger = null;
            this.PassengerCalls = new List<Passenger>();
            this.PassengersInLift = new List<Passenger>();
            this.TimeBetweenFloors = timebetweenfloors;
            this.CollectionTime = collectiontime;
            this.DropTime = droptime;
            this.Capacity = capacity;
            this.TargetFloor = null;
            this.TimeToCollectionEnd = null;
            this.TimeToDropEnd = null;
            this.TimeToNextFloor = null;
            this.DroppingPassenger = null;
            this.CollectingPassenger = null;
            this.EmergencyTarget = null;
        }

    }

    // Define a corresponding LiftState enum.

    public enum LiftState
    {
        Idle,
        MovingToCollect,
        MovingToDrop,
        Collecting,
        Dropping,
        CollectingExtra,
        DroppingExtra,
        EmergencyRetreating,
        EmergencyDropping,
        EmergencyReturning,
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
                int pass;
                if (lift.TargetPassenger != null)
                {
                    pass = lift.TargetPassenger.Id;
                }
                else
                {
                    pass = 0;
                }
                var liftPassengerIds = from p in lift.PassengersInLift
                                        select p.Id;
                string stringpassengersinlift = String.Join(",", liftPassengerIds);


                LogEntry newEntry = new LogEntry(currentTime, lift.CurrentFloor, eventDescription, pass, stringpassengersinlift);
                log.Add(newEntry);
            }


            // Create methods to ease passing arguments into Addlog().

            string StateChange()
            {
                return $"Lift changed state to {lift.State}";
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
                // The lift selects the "next" call to target.
                // The lift updates its TargetPassenger and TargetFloor.
                // The lift moves into the MovingToCollect state.
                // The lift checks whether its TargetFloor is the CurrentFloor.
                // There are two cases:

                // (2.A) The TargetFloor is the CurrentFloor.
                // Set TimeToNextFloor to 0.

                // (2.B) The TargetFloor is not the CurrentFloor.
                // Set TimeToNextFloor to TimeBetweenFloors.

                if (lift.State == LiftState.Idle)
                {
                    if (lift.PassengerCalls.Count == 0)     // (1)
                    {
                        continue;
                    }
                    else                                    // (2)
                    {
                        lift.TargetPassenger = (from p in lift.PassengerCalls
                                                orderby p.StartTime, p.Id
                                                select p).First();          // lift.TargetPassenger = lift.PassengersCalls[0] would suffice given your ordering of the input csv rows.
                        lift.TargetFloor = lift.TargetPassenger.StartFloor;
                        lift.State = LiftState.MovingToCollect;
                        AddLog(StateChange());

                        if (lift.TargetFloor == lift.CurrentFloor)
                        {
                            lift.TimeToNextFloor = 0;
                        }
                        else
                        {
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                        }
                    }
                }



                // === Check if the lift is in the MovingToCollect state or MovingToDrop state ===

                // At this point in the loop, when, and only when the lift is in one of these states, it will have a non-null TimeToNextFloor value.

                // If the lift is in one of the states, we check whether TimeToNextFloor is 0.
                // There are two cases.

                // (1) TimeToNextFloor is not 0.
                // This represents that the lift is not at the next floor.
                // The lift moves.
                // We decrement TimeToNextFloor by 1 and skip the rest of the loop.

                // (2) TimeToNextFloor has reached 0.
                // This represents that the lift is at the next floor. We update our CurrentFloor.
                // There are two sub-cases:

                // (2.A) The CurrentFloor is not the TargetFloor.
                // The lift now checks to see whether there are extra Passengers to drop off at the CurrentFloor.
                // There are two sub-cases:

                // (2.A.i) There are extra Passengers to drop off at the CurrentFloor.
                // The lift moves into the DroppingExtra state and begins to drop off the "selected" internal Passenger.
                // TimeToNextFloor is set to null.
                // The DroppingPassenger is updated to the selected Passenger.
                // The TimeToDropEnd is set to DroppingTime.
                // TimeToDropEnd is decremented by 1 and the rest of the loop is skipped.


                // (2.A.ii) There are no extra Passengers to drop off at the CurrentFloor.
                // The lift now checks to see whether that are extra Passengers to collect at the CurrentFloor (and whether it has capacity for at least one).
                // There are two sub-cases:

                // (2.A.ii.a) There are extra Passengers to collect at the CurrentFloor and the lift has capacity for at least one.
                // The lift moves into the CollectingExtra state and begins to collect the "selected" external Passenger.
                // TimeToNextFloor is set to null.
                // The CollectingPassenger is updated to the selected Passenger.
                // The TimeToCollectionEnd is set to CollectionTime.
                // TimeToCollectionEnd is decremented by 1 and the rest of the loop is skipped.

                // (2.A.ii.b) There are no extra Passengers to collect at the CurrentFloor or the lift doesn't have capacity for at least one.
                // The lift begins moving to the next floor on its ways to the TargetFloor.
                // We reset TimeToNextFloor to TimeBetweenFloors, then decrement by 1 and skip the rest of the loop.


                // (2.B) The CurrentFloor is the TargetFloor.
                // We check whether the lift was in the MovingToDrop or MovingToCollect state.

                // (2.B.i) If the lift was in the MovingToDrop state, it now moves into the Dropping state (we prioritise the TargetPassenger drop over extra passengers).
                // TimeToNextFloor is set to null.
                // Set the DroppingPassenger to the TargetPassenger.
                // Set the TimeToDropEnd to DropTime.
                // Decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2.B.ii) If the lift was in the MovingToCollect state, it first checks to see whether there are extra Passengers to drop off at the CurrentFoor.
                // There are two sub-cases:

                // (2.B.ii.a) There are extra Passengers to drop off at the CurrentFloor.
                // The lift moves into the DroppingExtra state and begins to drop off the selected internal Passenger.
                // Note, we prioritise dropping extra internal Passengers over collecting the TargetPassenger.
                // TimeToNextFloor is set to null.
                // The DroppingPassenger is updated to the selected Passenger.
                // The TimeToDropEnd is set to DroppingTime.
                // TimeToDropEnd is decremented by 1 and the rest of the loop is skipped.


                // (2.B.ii.b) There are no extra Passengers to drop off at the CurrentFloor.
                // The lift now checks to see whether it has capacity for the TargetPassenger.
                // There are two cases.

                // (2.B.ii.b.i) The lift is at capacity i.e. lift.Capacity == lift.PassengersInLift.Count.
                // The lift moves into the EmergencyRetreat state.
                // The EmergencyTarget is set (the internal passenger whose DestFloor is closest to CurrentFloor).
                // The TimeToNextFloor is set to TimeBetweenFloors.
                // TimeToNextFloor is decremented by 1 and the rest of the loop is skipped.

                // (2.B.ii.b.ii) The lift is not at capacity.
                // The lift moves into the Collecting state (we prioritise the TargetPassenger collection over extra collections).
                // TimeToNextFloor is set to null.
                // Set the CollectingPassenger to the TargetPassenger.
                // The TimeToCollectionEnd is set to CollectionTime.
                // TimeToCollectionEnd is decremented by 1 and the rest of the loop is skipped.

                if (lift.State == LiftState.MovingToCollect || lift.State == LiftState.MovingToDrop)
                {
                    if (lift.TimeToNextFloor != 0)      // (1)
                    {
                        lift.TimeToNextFloor -= 1;
                        continue;
                    }
                    else                                // (2)
                    {
                        if (lift.CurrentFloor < lift.TargetFloor)
                        {
                            lift.CurrentFloor += 1;

                        }
                        else if (lift.CurrentFloor > lift.TargetFloor)
                        {
                            lift.CurrentFloor -= 1;
                        }
                        AddLog(FloorArrival());

                        if (lift.CurrentFloor != lift.TargetFloor)              // (2.A)
                        {
                            var extrasToDrop = (from p in lift.PassengersInLift
                                                where p.DestFloor == lift.CurrentFloor
                                                select p).ToList();

                            if (extrasToDrop.Count > 0)                         // (2.A.i)
                            {
                                lift.DroppingPassenger = (from p in extrasToDrop
                                                          orderby p.StartTime, p.Id
                                                          select p).First();
                                lift.State = LiftState.DroppingExtra;
                                AddLog(StateChange());
                                lift.TimeToNextFloor = null;
                                lift.TimeToDropEnd = lift.DropTime;
                                lift.TimeToDropEnd -= 1;
                                continue;
                            }
                            else                                                // (2.A.ii)
                            {
                                var extrasToCollect = (from p in lift.PassengerCalls
                                                       where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                                       select p).ToList();

                                if (extrasToCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)             // (2.A.ii.a)
                                {
                                    lift.CollectingPassenger = (from p in extrasToCollect
                                                                orderby p.StartTime, p.Id
                                                                select p).First();
                                    lift.State = LiftState.CollectingExtra;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = null;
                                    lift.TimeToCollectionEnd = lift.CollectionTime;
                                    lift.TimeToCollectionEnd -= 1;
                                    continue;
                                }
                                else                                            // (2.A.ii.b)
                                {
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                            }
                        }
                        else                                                    // (2.B)
                        {
                            if (lift.State == LiftState.MovingToDrop)           // (2.B.i)
                            {
                                lift.State = LiftState.Dropping;
                                lift.TimeToNextFloor = null;
                                lift.DroppingPassenger = lift.TargetPassenger;
                                AddLog(StateChange());
                                lift.TimeToDropEnd = lift.DropTime;
                                lift.TimeToDropEnd -= 1;
                                continue;
                            }
                            else if (lift.State == LiftState.MovingToCollect)   // (2.B.ii)
                            {
                                var extrasToDrop = (from p in lift.PassengersInLift
                                                    where p.DestFloor == lift.CurrentFloor
                                                    select p).ToList();

                                if (extrasToDrop.Count > 0)                     // (2.B.ii.a)
                                {
                                    lift.DroppingPassenger = (from p in extrasToDrop
                                                              orderby p.StartTime, p.Id
                                                              select p).First();
                                    lift.State = LiftState.DroppingExtra;
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = null;
                                    lift.TimeToDropEnd = lift.DropTime;
                                    lift.TimeToDropEnd -= 1;
                                    continue;
                                }
                                else                                            // (2.B.ii.b)
                                {
                                    if (lift.Capacity == lift.PassengersInLift.Count)   // (2.B.ii.b.i)
                                    {
                                        lift.State = LiftState.EmergencyRetreating;
                                        lift.EmergencyTarget = (from p in lift.PassengersInLift
                                                                orderby Math.Abs(lift.CurrentFloor - p.DestFloor)
                                                                select p).First();
                                        AddLog(StateChange());
                                        lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                        lift.TimeToNextFloor -= 1;
                                        continue;

                                    }
                                    else                                                // (2.B.ii.b.ii)
                                    {
                                        lift.State = LiftState.Collecting;
                                        lift.TimeToNextFloor = null;
                                        lift.CollectingPassenger = lift.TargetPassenger;
                                        AddLog(StateChange());
                                        lift.TimeToCollectionEnd = lift.CollectionTime;
                                        lift.TimeToCollectionEnd -= 1;
                                        continue;
                                    }
                                }
                            }
                        }

                    }
                }



                // === Check if the lift is in the Dropping state ===

                // In this state, the lift is dropping its TargetPassenger.
                // If it is, we check whether TimeToDropEnd is 0.
                // There are two cases:

                // (1) TimeToDropEnd is not 0.
                // We decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2) TimeToDropEnd is 0.
                // We update TimeToDropEnd, TargetFloor, and DroppingPassenger to null.
                // We add the TargetPassenger to servedPassengers, remove it from lift.PassengerCalls and lift.PassengersInLift, and update TargetPassenger to null.
                // Next, we need to find a new TargetPassenger.
                // We check whether lift.PassengerCalls is empty.
                // There are two cases.

                // (2.A) lift.PassengerCalls is empty.
                // The lift moves into the Idle state.
                // Skip the rest of the loop (this implements the protocol to remain stationary when there are no calls).

                // (2.B) lift.PassengerCalls is non-empty.
                // The lift selects the "next" call to serve in lift.PassengerCalls, updating its TargetPassenger.
                // Note that the TargetPassenger may or may not be in lift.PassengersInLift. We check this.
                // We have two cases:


                // (2.B.i) The TargetPassenger is in the lift.
                // We update the TargetFloor to the TargetPassenger's DestFloor.
                // We update the state of the lift to MovingToDrop.
                // Next, we check whether the TargetFloor is the CurrentFloor.
                // There are two cases.

                // (2.B.i.a) The TargetFloor is the CurrentFloor.
                // The lift moves into the Dropping state.
                // Set the lift's TimeToDropEnd to DropTime.
                // Set DroppingPassenger to TargetPassenger.
                // Decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2.B.i.b) The TargetFloor is not the CurrentFloor.
                // We must still check whether there are extra internal passengers to drop, and extra external passengers to collect, at the CurrentFloor.
                // We first check whether there are extra internal passengers to drop at the CurrentFloor.
                // There are two cases.

                // (2.B.i.b.i) There are extra internal passengers to drop at the CurrentFloor.
                // The lift moves into the DroppingExtra state.
                // We select one of the extra internal passengers.
                // The DroppingPassenger is updated to this selected passenger.
                // Set TimeToDropEnd to DropTime.
                // Decrement TimeToDropEnd by 1 and skip the rest of the loop.


                // (2.B.i.b.ii) There are no extra internal passengers to drop at the CurrentFloor.
                // The lift checks whether there are extra external passengers to collect at the CurrentFloor.
                // There are two cases.

                // (2.B.i.b.ii.A) There are extra external passengers to collect at the CurrentFloor.
                // The lift moves into the CollectingExtra state.
                // We select one of the extra external passengers.
                // The CollectingPassenger is updated to this selected passenger.
                // We set TimeToCollectionEnd to CollectionTime.
                // Decrement TimeToCollectionEnd by 1 and skip the rest of the loop.


                // (2.B.i.b.ii.B) There are no extra external passengers to collect at the CurrentFloor.
                // The lift begins to move to serve the TargetPassenger.
                // Set TimeToNextFloor to TimeBetweenFloors.
                // Decrement TimeToNextFloor by 1 and skip the rest of the loop.




                // (2.B.ii) The TargetPassenger is not in the lift.
                // We update the TargetFloor to the TargetPassenger's StartFloor.
                // We update the status of the lift to MovingToCollect.
                // We first must check whether there are extra internal passengers to drop at the CurrentFloor.
                // There are two cases.

                // (2.B.ii.a) There are extra internal passengers to drop at the CurrentFloor.
                // The lift moves into the DroppingExtra state.
                // We select one of the extra internal passengers.
                // The DroppingPassenger is updated to this selected passenger.
                // Set TimeToDropEnd to DropTime.
                // Decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2.B.ii.b) There are no extra internal passengers to drop at the CurrentFloor.
                // We now check whether the TargetFloor is the CurrentFloor.
                // There are two cases.

                // (2.B.ii.b.A) The TargetFloor is the CurrentFloor.
                // Note we don't have to check for capacity here as we have just dropped the last TargetPassenger off!
                // The lift moves into the Collecting state.
                // The CollectingPassenger is updated to the TargetPassenger.
                // We set TimeToCollectionEnd to CollectionTime.
                // Decrement TimeToCollectionEnd by 1 and skip the rest of the loop.


                // (2.B.ii.b.B) The TargetFloor is not the CurrentFloor.
                // We must now check whether there are extra external passengers to collect at the CurrentFloor.
                // Note we don't have to check for capacity here as we have just dropped the last TargetPassenger off!
                // There are two cases.

                // (2.B.ii.b.B.i) There are extra external passengers to collect at the CurrentFloor.
                // The lift moves into the CollectingExtra state.
                // We select one of the extra external passengers.
                // The CollectingPassenger is updated to this selected passenger.
                // We set TimeToCollectionEnd to CollectionTime.
                // Decrement TimeToCollectionEnd by 1 and skip the rest of the loop.

                // (2.B.ii.b.B.ii) There are no extra external passengers to collect at the CurrentFloor.
                // The lift begins to move to serve the TargetPassenger.
                // Set TimeToNextFloor to TimeBetweenFloors.
                // Decrement TimeToNextFloor by 1 and skip the rest of the loop.


                if (lift.State == LiftState.Dropping)
                {
                    if (lift.TimeToDropEnd != 0)                           // (1)
                    {
                        lift.TimeToDropEnd -= 1;
                        continue;
                    }
                    else                                                    // (2)
                    {
                        servedPassengers.Add(lift.TargetPassenger!);
                        lift.PassengerCalls.Remove(lift.TargetPassenger!);
                        lift.PassengersInLift.Remove(lift.TargetPassenger!);
                        AddLog(PassengerDrop());
                        lift.DroppingPassenger = null;
                        lift.TimeToDropEnd = null;
                        lift.TargetPassenger = null;
                        lift.TargetFloor = null;

                        if (lift.PassengerCalls.Count == 0)                 // (2.A)
                        {
                            lift.State = LiftState.Idle;
                            AddLog(StateChange());
                            continue;
                        }
                        else                                                // (2.B)
                        {
                            lift.TargetPassenger = (from p in lift.PassengerCalls
                                                    orderby p.StartTime, p.Id
                                                    select p).First();

                            if (lift.PassengersInLift.Contains(lift.TargetPassenger))       // (2.B.i)
                            {
                                lift.TargetFloor = lift.TargetPassenger.DestFloor;
                                lift.State = LiftState.MovingToDrop;
                                AddLog(StateChange());

                                if (lift.TargetFloor == lift.CurrentFloor)                  // (2.B.i.a)
                                {
                                    lift.State = LiftState.Dropping;
                                    lift.DroppingPassenger = lift.TargetPassenger;
                                    AddLog(StateChange());
                                    lift.TimeToDropEnd = lift.DropTime;
                                    lift.TimeToDropEnd -= 1;
                                    continue;
                                }
                                else                                                        // (2.B.i.b)
                                {
                                    var extrasToDrop = (from p in lift.PassengersInLift
                                                        where p.DestFloor == lift.CurrentFloor
                                                        select p).ToList();

                                    if (extrasToDrop.Count > 0)                             // (2.B.i.b.i)
                                    {
                                        lift.DroppingPassenger = (from p in extrasToDrop
                                                                  orderby p.StartTime, p.Id
                                                                  select p).First();
                                        lift.State = LiftState.DroppingExtra;
                                        AddLog(StateChange());
                                        lift.TimeToDropEnd = lift.DropTime;
                                        lift.TimeToDropEnd -= 1;
                                        continue;
                                    }
                                    else                                                    // (2.B.i.b.ii)
                                    {
                                        var extrasToCollect = (from p in lift.PassengerCalls
                                                               where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                                               select p).ToList();

                                        if (extrasToCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)           // (2.B.i.b.ii.A)
                                        {
                                            lift.CollectingPassenger = (from p in extrasToCollect
                                                                        orderby p.StartTime, p.Id
                                                                        select p).First();
                                            lift.State = LiftState.CollectingExtra;
                                            AddLog(StateChange());
                                            lift.TimeToCollectionEnd = lift.CollectionTime;
                                            lift.TimeToCollectionEnd -= 1;
                                            continue;
                                        }
                                        else                                                // (2.B.i.b.ii.B)
                                        {
                                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                            lift.TimeToNextFloor -= 1;
                                            continue;
                                        }
                                    }
                                }
                            }
                            else                                                            // (2.B.ii)
                            {
                                lift.TargetFloor = lift.TargetPassenger.StartFloor;
                                lift.State = LiftState.MovingToCollect;
                                AddLog(StateChange());

                                var extrasToDrop = (from p in lift.PassengersInLift
                                                    where p.DestFloor == lift.CurrentFloor
                                                    select p).ToList();

                                if (extrasToDrop.Count > 0)                                 // (2.B.ii.a)
                                {
                                    lift.DroppingPassenger = (from p in extrasToDrop
                                                              orderby p.StartTime, p.Id
                                                              select p).First();
                                    lift.State = LiftState.DroppingExtra;
                                    AddLog(StateChange());
                                    lift.TimeToDropEnd = lift.DropTime;
                                    lift.TimeToDropEnd -= 1;
                                    continue;
                                }
                                else                                                        // (2.B.ii.b)
                                {
                                    if (lift.CurrentFloor == lift.TargetFloor)              // (2.B.ii.b.A)
                                    {
                                        lift.State = LiftState.Collecting;
                                        lift.CollectingPassenger = lift.TargetPassenger;
                                        AddLog(StateChange());
                                        lift.TimeToCollectionEnd = lift.CollectionTime;
                                        lift.TimeToCollectionEnd -= 1;
                                        continue;
                                    }
                                    else                                                    // (2.B.ii.b.B)
                                    {
                                        var extrasToCollect = (from p in lift.PassengerCalls
                                                               where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                                               select p).ToList();

                                        if (extrasToCollect.Count > 0)     // (2.B.ii.b.B.i)
                                        {
                                            lift.CollectingPassenger = (from p in extrasToCollect
                                                                        orderby p.StartTime, p.Id
                                                                        select p).First();
                                            lift.State = LiftState.CollectingExtra;
                                            AddLog(StateChange());
                                            lift.TimeToCollectionEnd = lift.CollectionTime;
                                            lift.TimeToCollectionEnd -= 1;
                                            continue;
                                        }
                                        else                                                // (2.B.ii.b.B.ii)
                                        {
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






                // === Check if the lift is in the Collecting state ===

                // In this state, the lift is collecting its TargetPassenger.
                // If it is, we check whether TimeToCollectionEnd is 0.
                // There are two cases:

                // (1) TimeToCollectionEnd is not 0.
                // We decrement TimeToCollectionEnd by 1 and skip the rest of the loop.

                // (2) TimeToCollectionEnd is 0.
                // TimeToCollectionEnd is set to null.
                // The TargetPassenger is added to PassengersInLift.
                // Update the TargetFloor to the TargetPassenger.DestFloor.
                // The lift would only have moved into the Collecting state if there were no extra internal Passengers that want to be dropped at the CurrentFloor.
                // However, there may be extra external Passengers that want to be collected at the CurrentFloor.
                // So, we check for these extra Passengers (and that we have capacity for at least one).
                // There are two cases:

                // (2.A) There are extra Passengers to collect at CurrentFloor and we have capacity for at least one.
                // The lift moves into the CollectingExtra state.
                // We select one of the extra external passengers.
                // The CollectingPassenger is updated to this selected passenger.
                // We set TimeToCollectionEnd to CollectionTime.
                // Decrement TimeToCollectionEnd by 1 and skip the rest of the loop.

                // (2.B) There are no extra Passengers to collect at CurrentFloor or we don't have any capacity.
                // The lift moves into the MovingToDrop state.
                // We update the TargetFloor.
                // We earlier checked that for no Passenger is StartFloor equal to DestFloor, so the TargetFloor will be distinct from the CurrentFloor.
                // We set TimeToNextFloor at TimeBetweenFloors.
                // We decrement TimeToNextFloor by 1 and skip the rest of the loop.

                if (lift.State == LiftState.Collecting)
                {
                    if (lift.TimeToCollectionEnd != 0)                                      // (1)
                    {
                        lift.TimeToCollectionEnd -= 1;
                        continue;
                    }
                    else                                                                    // (2)                                              
                    {
                        lift.PassengersInLift.Add(lift.TargetPassenger!);
                        AddLog(PassengerCollection());
                        lift.CollectingPassenger = null;
                        lift.TimeToCollectionEnd = null;
                        lift.TargetFloor = lift.TargetPassenger!.DestFloor;

                        var extrasToCollect = (from p in lift.PassengerCalls
                                               where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                               select p).ToList();
                        if (extrasToCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)        // (2.A)
                        {
                            lift.CollectingPassenger = (from p in extrasToCollect
                                                        orderby p.StartTime, p.Id
                                                        select p).First();
                            lift.State = LiftState.CollectingExtra;
                            AddLog(StateChange());
                            lift.TimeToCollectionEnd = lift.CollectionTime;
                            lift.TimeToCollectionEnd -= 1;
                            continue;
                        }
                        else                                                                // (2.B)
                        {
                            lift.State = LiftState.MovingToDrop;
                            AddLog(StateChange());
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
                        }
                    }
                }



                // === Check if the lift is in the DroppingExtra state ===
                // If the lift is in the DroppingExtra state, we first check if TimeToDropEnd is 0.
                // We have two cases.

                // (1) TimeToDropEnd is not 0.
                // Decrement TimeToDropEnd by 1 and skip the rest of the loop.

                // (2) TimeToDropEnd is 0.
                // We set TimeToDropEnd to null.
                // We add the lift.DroppingPassenger to servedPassengers.
                // We remove the lift.DroppingPassenger from PassengerCalls and PassengersInLift.
                // We set the DroppingPassenger to null.
                // We first check if there are extra internal passengers to drop at the CurrentFloor.
                // There are two cases.

                // (2.A) There are extra internal passengers to drop at the CurrentFloor.
                // We select the next such passenger to drop.
                // The lift stays in the DroppingExtra state.
                // We update DroppingPassenger with the selected Passenger.
                // We set TimeToDropEnd to DroppingTime.
                // We decrement TimeToDropEnd by 1 and skip the loop.

                // (2.B) There are no extra internal passengers to drop at the CurrentFloor.
                // Now we check whether (the TargetPassenger needs to be collected and we are at the TargetFloor).
                // There are two cases.

                // (2.B.i) The TargetPassenger needs to be collected and we are at the TargetFloor.
                // We update the CollectingPassenger with the TargetPassenger.
                // The lift moves into the Collecting state.
                // Note we don't have to check for capacity here as we have just dropped off an extra internal passenger!
                // We set TimeToCollectionEnd to CollectionTime.
                // We decrement TimeToCollectionEnd by 1 and skip the loop.

                // (2.B.ii) Either the TargetPassenger does not need to be collected or we are not at the TargetFloor.
                // We check whether there are extra external passengers to collect at the CurrentFloor.
                // There are two cases.

                // (2.B.ii.a) There are extra external passengers to collect at the CurrentFloor.
                // We select the next such passenger to collect.
                // We update CollectingPassenger with the selected Passenger.
                // The lift moves into the CollectingExtra state.
                // Note we don't have to check for capacity here as we have just dropped off an extra internal passenger!
                // We set TimeToCollectionEnd to CollectionTime.
                // We decrement TimeToCollectionEnd by 1 and skip the loop.

                // (2.B.ii.b) There are no extra external passengers to collect at the CurrentFloor.
                // We check whether the TargetPassenger is in PassengersInLift.
                // If it is, the lift moves to the MovingToDrop state.
                // If it is not, the lift moves to the MovingToCollect state.
                // Either way, we set TimeToNextFloor to TimeBetweenFloors.
                // Decrement TimeToNextFloor and skip the loop.


                if (lift.State == LiftState.DroppingExtra)
                {
                    if (lift.TimeToDropEnd != 0)                    // (1)
                    {
                        lift.TimeToDropEnd -= 1;
                        continue;
                    }
                    else                                            // (2)
                    {
                        servedPassengers.Add(lift.DroppingPassenger!);
                        lift.PassengersInLift.Remove(lift.DroppingPassenger!);
                        lift.PassengerCalls.Remove(lift.DroppingPassenger!);
                        AddLog(PassengerDrop());
                        lift.DroppingPassenger = null;
                        lift.TimeToDropEnd = null;

                        var extrasToDrop = (from p in lift.PassengersInLift
                                            where p.DestFloor == lift.CurrentFloor
                                            select p).ToList();

                        if (extrasToDrop.Count > 0)              // (2.A)
                        {
                            lift.DroppingPassenger = (from p in extrasToDrop
                                                      orderby p.StartTime, p.Id
                                                      select p).First();
                            lift.State = LiftState.DroppingExtra;
                            AddLog(StateChange());
                            lift.TimeToDropEnd = lift.DropTime;
                            lift.TimeToDropEnd -= 1;
                            continue;
                        }
                        else                                        // (2.B)
                        {
                            if (lift.TargetFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(lift.TargetPassenger!))     // (2.B.i)
                            {
                                lift.CollectingPassenger = lift.TargetPassenger;
                                lift.State = LiftState.Collecting;
                                AddLog(StateChange());
                                lift.TimeToCollectionEnd = lift.CollectionTime;
                                lift.TimeToCollectionEnd -= 1;
                                continue;
                            }
                            else                                                                                                    // (2.B.ii)
                            {
                                var extrasToCollect = (from p in lift.PassengerCalls
                                                       where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                                       select p).ToList();

                                if (extrasToCollect.Count > 0)                                                                      // (2.B.ii.a)
                                {
                                    lift.CollectingPassenger = (from p in extrasToCollect
                                                                orderby p.StartTime, p.Id
                                                                select p).First();
                                    lift.State = LiftState.CollectingExtra;
                                    AddLog(StateChange());
                                    lift.TimeToCollectionEnd = lift.CollectionTime;
                                    lift.TimeToCollectionEnd -= 1;
                                    continue;
                                }
                                else                                                                                                // (2.B.ii.b)
                                {
                                    if (lift.PassengersInLift.Contains(lift.TargetPassenger!))
                                    {
                                        lift.State = LiftState.MovingToDrop;
                                    }
                                    else
                                    {
                                        lift.State = LiftState.MovingToCollect;
                                    }
                                    AddLog(StateChange());
                                    lift.TimeToNextFloor = lift.TimeBetweenFloors;
                                    lift.TimeToNextFloor -= 1;
                                    continue;
                                }
                            }
                        }

                    }
                }




                // === Check if the lift is in the CollectingExtra state === 

                // If the lift is in the CollectingExtra state, we first check its TimeToCollectionEnd.
                // We have two cases.

                // (1) TimeToCollectionEnd is not 0.
                // Decrement TimeToCollectionEnd by 1 and skip the rest of the loop.

                // (2) TimeToCollectionEnd is 0.
                // We set TimeToCollectionEnd to null.
                // We add the lift.CollectingPassenger to lift.PassengersInLift.
                // We set the CollectingPassenger to null.
                // We first check if there are extra external passengers to collect and the lift has capacity for at least one.
                // Two cases.

                // (2.A)
                // There are extra external passengers to collect and the lift has capacity for at least one.
                // We select the next such passenger to collect.
                // We update CollectingPassenger with the selected Passenger.
                // The lift moves into the CollectingExtra state.
                // We set TimeToCollectionEnd to CollectionTime.
                // We decrement TimeToCollectionEnd by 1 and skip the loop.

                // (2.B)
                // There are no extra external passengers to collect or the lift does not have capacity for one.
                // We check whether the TargetPassenger is in PassengersInLift.
                // If it is, the lift moves to the MovingToDrop state.
                // If it is not, the lift moves to the MovingToCollect state.
                // Either way, we set TimeToNextFloor to TimeBetweenFloors.
                // Decrement TimeToNextFloor and skip the loop.


                if (lift.State == LiftState.CollectingExtra)
                {
                    if (lift.TimeToCollectionEnd != 0)                              // (1)
                    {
                        lift.TimeToCollectionEnd -= 1;
                        continue;
                    }
                    else                                                            // (2)
                    {
                        lift.TimeToCollectionEnd = null;
                        lift.PassengersInLift.Add(lift.CollectingPassenger!);
                        AddLog(PassengerCollection());
                        lift.CollectingPassenger = null;

                        var extrasToCollect = (from p in lift.PassengerCalls
                                               where p.StartFloor == lift.CurrentFloor && !lift.PassengersInLift.Contains(p)
                                               select p).ToList();

                        if (extrasToCollect.Count > 0 && lift.PassengersInLift.Count < lift.Capacity)  // (2.A)
                        {
                            lift.CollectingPassenger = (from p in extrasToCollect
                                                        orderby p.StartTime, p.Id
                                                        select p).First();
                            lift.State = LiftState.CollectingExtra;
                            AddLog(StateChange());
                            lift.TimeToCollectionEnd = lift.CollectionTime;
                            lift.TimeToCollectionEnd -= 1;
                            continue;
                        }
                        else                                                        // (2.B)
                        {
                            if (lift.PassengersInLift.Contains(lift.TargetPassenger!))
                            {
                                lift.State = LiftState.MovingToDrop;
                            }
                            else
                            {
                                lift.State = LiftState.MovingToCollect;
                            }
                            AddLog(StateChange());
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
                        }
                    }
                }



                if (lift.State == LiftState.EmergencyRetreating)
                {
                    if (lift.TimeToNextFloor != 0)
                    {
                        lift.TimeToNextFloor -= 1;
                        continue;
                    }
                    else
                    {
                        if (lift.CurrentFloor < lift.EmergencyTarget!.DestFloor)
                        {
                            lift.CurrentFloor += 1;

                        }
                        else if (lift.CurrentFloor > lift.EmergencyTarget.DestFloor)
                        {
                            lift.CurrentFloor -= 1;
                        }
                        AddLog(FloorArrival());

                        if (lift.CurrentFloor == lift.EmergencyTarget.DestFloor)
                        {
                            lift.State = LiftState.EmergencyDropping;
                            lift.TimeToNextFloor = null;
                            lift.DroppingPassenger = lift.EmergencyTarget;
                            AddLog(StateChange());
                            lift.TimeToDropEnd = lift.DropTime;
                            lift.TimeToDropEnd -= 1;
                            continue;
                        }
                        else
                        {
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
                        }
                    }
                }


                if (lift.State == LiftState.EmergencyDropping)
                {
                    if (lift.TimeToDropEnd != 0)
                    {
                        lift.TimeToDropEnd -= 1;
                        continue;
                    }
                    else
                    {
                        lift.PassengersInLift.Remove(lift.DroppingPassenger!);
                        lift.PassengerCalls.Remove(lift.DroppingPassenger!);
                        servedPassengers.Add(lift.DroppingPassenger!);
                        AddLog(PassengerDrop());
                        lift.TimeToDropEnd = null;
                        lift.DroppingPassenger = null;
                        lift.EmergencyTarget = null;
                        lift.State = LiftState.EmergencyReturning;
                        AddLog(StateChange());
                        lift.TimeToNextFloor = lift.TimeBetweenFloors;
                        lift.TimeToNextFloor -= 1;
                        continue;
                    }
                }



                if (lift.State == LiftState.EmergencyReturning)
                {
                    if (lift.TimeToNextFloor != 0)
                    {
                        lift.TimeToNextFloor -= 1;
                        continue;
                    }
                    else
                    {
                        if (lift.CurrentFloor < lift.TargetPassenger!.StartFloor)
                        {
                            lift.CurrentFloor += 1;

                        }
                        else if (lift.CurrentFloor > lift.TargetPassenger.StartFloor)
                        {
                            lift.CurrentFloor -= 1;
                        }
                        AddLog(FloorArrival());

                        if (lift.CurrentFloor == lift.TargetPassenger.StartFloor)
                        {
                            lift.State = LiftState.Collecting;
                            lift.CollectingPassenger = lift.TargetPassenger;
                            lift.TimeToNextFloor = null;
                            lift.TimeToCollectionEnd = lift.CollectionTime;
                            lift.TimeToCollectionEnd -= 1;
                            continue;
                        }
                        else
                        {
                            lift.TimeToNextFloor = lift.TimeBetweenFloors;
                            lift.TimeToNextFloor -= 1;
                            continue;
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