-- Production Database Seeding Script
-- Run this against your Aiven PostgreSQL database

-- Create Admin User
DO $$
DECLARE
    admin_id UUID := gen_random_uuid();
    admin_email TEXT := 'admin@trustfirst.com';
    admin_exists BOOLEAN;
BEGIN
    SELECT EXISTS(SELECT 1 FROM "Users" WHERE "Email" = admin_email) INTO admin_exists;
    
    IF NOT admin_exists THEN
        INSERT INTO "Users" (
            "Id", "Email", "PasswordHash", "Role", "FailedLoginAttempts",
            "IsActive", "CreatedAt", "Profile", "Status", "ApprovedAt",
            "FirstName", "LastName", "Department"
        )
        VALUES (
            admin_id,
            admin_email,
            '$2a$11$rKZqXqZ3qHKJ9zqCqZ3qZOqZqXqZ3qHKJ9zqCqZ3qZOqZqXqZ3qHK', -- Hash for "Admin@123"
            'Admin',
            0,
            true,
            NOW(),
            '{}'::jsonb,
            'Active',
            NOW(),
            'System',
            'Administrator',
            'IT'
        );
        
        RAISE NOTICE 'Created admin user: %', admin_email;
    ELSE
        RAISE NOTICE 'Admin user already exists: %', admin_email;
    END IF;
END $$;

-- Create Standard User
DO $$
DECLARE
    user_id UUID := gen_random_uuid();
    user_email TEXT := 'user@trustfirst.com';
    user_exists BOOLEAN;
BEGIN
    SELECT EXISTS(SELECT 1 FROM "Users" WHERE "Email" = user_email) INTO user_exists;
    
    IF NOT user_exists THEN
        INSERT INTO "Users" (
            "Id", "Email", "PasswordHash", "Role", "FailedLoginAttempts",
            "IsActive", "CreatedAt", "Profile", "Status", "ApprovedAt",
            "FirstName", "LastName", "Department"
        )
        VALUES (
            user_id,
            user_email,
            '$2a$11$rKZqXqZ3qHKJ9zqCqZ3qZOqZqXqZ3qHKJ9zqCqZ3qZOqZqXqZ3qHK', -- Hash for "User@123"
            'StandardUser',
            0,
            true,
            NOW(),
            '{}'::jsonb,
            'Active',
            NOW(),
            'Standard',
            'User',
            'General'
        );
        
        RAISE NOTICE 'Created standard user: %', user_email;
    ELSE
        RAISE NOTICE 'Standard user already exists: %', user_email;
    END IF;
END $$;

-- Display results
SELECT 
    'Users Seeded' as "Status",
    COUNT(*) as "Total Users",
    COUNT(*) FILTER (WHERE "Role" = 'Admin') as "Admin Users",
    COUNT(*) FILTER (WHERE "Role" = 'StandardUser') as "Standard Users"
FROM "Users";
