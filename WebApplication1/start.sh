#!/bin/bash

# Load Balancer Startup Script for ASP.NET Core Application
# This script starts 3 instances of your application on different ports

echo "?? Starting Load Balanced ASP.NET Core Application..."
echo "================================================"

# Function to check if port is available
check_port() {
    port=$1
    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
        echo "??  Port $port is already in use"
        return 1
    else
        echo "? Port $port is available"
        return 0
    fi
}

# Function to start application instance
start_instance() {
    port=$1
    profile=$2
    
    echo "?? Starting instance on port $port with profile $profile..."
    
    export ASPNETCORE_ENVIRONMENT=Development
    dotnet run --launch-profile $profile --urls http://0.0.0.0:$port > "logs/app-$port.log" 2>&1 &
    
    pid=$!
    echo $pid > "pids/app-$port.pid"
    echo "? Instance started on port $port (PID: $pid)"
    
    sleep 5
    
    if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:$port/ | grep -q "200\|302"; then
        echo "? Instance on port $port is responding"
    else
        echo "? Instance on port $port is not responding"
        echo "?? Last 20 lines of log for port $port:"
        cat "logs/app-$port.log" | tail -n 20
    fi
}

# Function to stop all instances
stop_all() {
    echo "?? Stopping all instances..."
    
    for pidfile in pids/app-*.pid; do
        if [ -f "$pidfile" ]; then
            pid=$(cat "$pidfile")
            if kill -0 "$pid" 2>/dev/null; then
                echo "?? Stopping process $pid"
                kill "$pid"
                sleep 1
                if kill -0 "$pid" 2>/dev/null; then
                    echo "?? Force stopping process $pid"
                    kill -9 "$pid"
                fi
                rm "$pidfile"
            fi
        fi
    done
    
    for port in 5000 5001 5002 5003 5025 7216 44253; do
        pid=$(lsof -t -i:$port 2>/dev/null)
        if [ ! -z "$pid" ]; then
            echo "?? Force stopping process on port $port (PID: $pid)"
            kill -9 "$pid" 2>/dev/null
        fi
    done
    
    echo "? All instances stopped"
}

# Function to show status
show_status() {
    echo "?? Application Status:"
    echo "===================="
    
    for port in 5001 5002 5003; do
        if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
            pid=$(lsof -t -i:$port)
            echo "? Port $port: RUNNING (PID: $pid)"
        else
            echo "? Port $port: NOT RUNNING"
        fi
    done
}

# Create necessary directories
mkdir -p logs pids

# Handle script arguments
case "$1" in
    "start")
        echo "?? Starting load balanced application..."
        
        ports_available=true
        for port in 5001 5002 5003; do
            if ! check_port $port; then
                ports_available=false
            fi
        done
        
        if [ "$ports_available" = false ]; then
            echo "? Some ports are already in use. Run './start.sh stop' first."
            exit 1
        fi
        
        start_instance 5001 "http-5001"
        sleep 5
        start_instance 5002 "http-5002"
        sleep 5
        start_instance 5003 "http-5003"
        
        echo ""
        echo "?? Load balanced application started successfully!"
        echo "?? Instance 1: http://127.0.0.1:5001"
        echo "?? Instance 2: http://127.0.0.1:5002"
        echo "?? Instance 3: http://127.0.0.1:5003"
        echo "?? Load Balancer (via Nginx): https://localhost (port 443)"
        echo ""
        echo "?? Use './start.sh status' to check running instances"
        echo "?? Use './start.sh stop' to stop all instances"
        echo "?? Logs are available in logs/ directory"
        ;;
        
    "stop")
        stop_all
        ;;
        
    "restart")
        echo "?? Restarting load balanced application..."
        stop_all
        sleep 2
        $0 start
        ;;
        
    "status")
        show_status
        ;;
        
    "logs")
        if [ -z "$2" ]; then
            echo "?? Available log files:"
            ls -la logs/app-*.log 2>/dev/null || echo "No log files found"
            echo ""
            echo "Usage: $0 logs <port>"
            echo "Example: $0 logs 5001"
        else
            port=$2
            logfile="logs/app-$port.log"
            if [ -f "$logfile" ]; then
                echo "?? Showing logs for port $port:"
                echo "================================"
                tail -f "$logfile"
            else
                echo "? Log file not found: $logfile"
            fi
        fi
        ;;
        
    *)
        echo "?? Load Balancer Control Script"
        echo "==============================="
        echo ""
        echo "Usage: $0 {start|stop|restart|status|logs [port]}"
        echo ""
        echo "Commands:"
        echo "  start    - Start all application instances (ports 5001, 5002, 5003)"
        echo "  stop     - Stop all application instances"
        echo "  restart  - Restart all application instances"
        echo "  status   - Show status of all instances"
        echo "  logs     - Show available logs or tail specific port log"
        echo ""
        echo "Examples:"
        echo "  $0 start         # Start load balanced app"
        echo "  $0 status        # Check which instances are running"
        echo "  $0 logs 5001     # View logs for instance on port 5001"
        echo "  $0 stop          # Stop all instances"
        ;;
esac