using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WebAPI.Models;

namespace WebAPI.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Assignment> Assignments { get; set; }

    public virtual DbSet<CreateJavaLobTable> CreateJavaLobTables { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerAppointment> CustomerAppointments { get; set; }

    public virtual DbSet<CustomerKey> CustomerKeys { get; set; }


    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EmployeeKey> EmployeeKeys { get; set; }


    public virtual DbSet<EmployeeShift> EmployeeShifts { get; set; }

    public virtual DbSet<OrderRequest> OrderRequests { get; set; }

    public virtual DbSet<Part> Parts { get; set; }

    public virtual DbSet<PartRequest> PartRequests { get; set; }

    public virtual DbSet<PartRequestItem> PartRequestItems { get; set; }

    public virtual DbSet<StockIn> StockIns { get; set; }

    public virtual DbSet<StockInItem> StockInItems { get; set; }

    public virtual DbSet<StockOut> StockOuts { get; set; }

    public virtual DbSet<StockOutItem> StockOutItems { get; set; }

    public virtual DbSet<UserOtpLog> UserOtpLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseOracle("User Id=App;Password=App;Data Source=10.147.20.157:1521/ORCLPDB1");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDefaultSchema("APP")
            .UseCollation("USING_NLS_COMP");

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(e => e.AssignId).HasName("SYS_C008344");

            entity.ToTable("ASSIGNMENT");

            entity.Property(e => e.AssignId)
                .HasColumnType("NUMBER")
                .HasColumnName("ASSIGN_ID");
            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.EndDate)
                .HasColumnType("DATE")
                .HasColumnName("END_DATE");
            entity.Property(e => e.OrderId)
                .HasColumnType("NUMBER")
                .HasColumnName("ORDER_ID");
            entity.Property(e => e.StartDate)
                .HasColumnType("DATE")
                .HasColumnName("START_DATE");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");

            entity.HasOne(d => d.Emp).WithMany(p => p.Assignments)
                .HasForeignKey(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ASSIGN_EMPLOYEE");

            entity.HasOne(d => d.Order).WithMany(p => p.Assignments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ASSIGN_ORDER");
        });

        modelBuilder.Entity<CreateJavaLobTable>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("CREATE$JAVA$LOB$TABLE");

            entity.HasIndex(e => e.Name, "SYS_C008311").IsUnique();

            entity.Property(e => e.Loadtime)
                .HasColumnType("DATE")
                .HasColumnName("LOADTIME");
            entity.Property(e => e.Lob)
                .HasColumnType("BLOB")
                .HasColumnName("LOB");
            entity.Property(e => e.Name)
                .HasMaxLength(700)
                .IsUnicode(false)
                .HasColumnName("NAME");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Phone).HasName("SYS_C008325");

            entity.ToTable("CUSTOMER");

            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("PHONE");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("ADDRESS");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(6)
                .HasDefaultValueSql("SYSTIMESTAMP")
                .HasColumnName("CREATED_AT");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("EMAIL");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("FULL_NAME");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("PASSWORD_HASH");
            entity.Property(e => e.PublicKey)
                .HasColumnType("CLOB")
                .HasColumnName("PUBLIC_KEY");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnName("STATUS");
        });

        modelBuilder.Entity<CustomerAppointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("SYS_C008401");

            entity.ToTable("CUSTOMER_APPOINTMENT");

            entity.Property(e => e.AppointmentId)
                .HasColumnType("NUMBER")
                .HasColumnName("APPOINTMENT_ID");
            entity.Property(e => e.AppointmentDate)
                .HasColumnType("DATE")
                .HasColumnName("APPOINTMENT_DATE");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CUSTOMER_PHONE");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValueSql("'SCHEDULED'")
                .HasColumnName("STATUS");

            entity.HasOne(d => d.CustomerPhoneNavigation).WithMany(p => p.CustomerAppointments)
                .HasForeignKey(d => d.CustomerPhone)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_APPOINTMENT_CUSTOMER");
        });

        modelBuilder.Entity<CustomerKey>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("SYS_C008328");

            entity.ToTable("CUSTOMER_KEYS");

            entity.Property(e => e.CustomerId)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CUSTOMER_ID");
            entity.Property(e => e.PrivateKey)
                .HasColumnType("CLOB")
                .HasColumnName("PRIVATE_KEY");

            entity.HasOne(d => d.Customer).WithOne(p => p.CustomerKey)
                .HasForeignKey<CustomerKey>(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_KEYS");
        });

        

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmpId).HasName("SYS_C008320");

            entity.ToTable("EMPLOYEE");

            entity.HasIndex(e => e.Username, "SYS_C008321").IsUnique();

            entity.HasIndex(e => e.Email, "SYS_C008322").IsUnique();

            entity.HasIndex(e => e.Phone, "SYS_C008323").IsUnique();

            entity.Property(e => e.EmpId)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(6)
                .HasDefaultValueSql("SYSTIMESTAMP")
                .HasColumnName("CREATED_AT");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("EMAIL");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("FULL_NAME");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("PASSWORD_HASH");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("PHONE");
            entity.Property(e => e.PublicKey)
                .HasColumnType("CLOB")
                .HasColumnName("PUBLIC_KEY");
            
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValueSql("'ACTIVE'")
                .HasColumnName("STATUS");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("USERNAME");
        });

        modelBuilder.Entity<EmployeeKey>(entity =>
        {
            entity.HasKey(e => e.EmpId).HasName("SYS_C008326");

            entity.ToTable("EMPLOYEE_KEYS");

            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.PrivateKey)
                .HasColumnType("CLOB")
                .HasColumnName("PRIVATE_KEY");

            entity.HasOne(d => d.Emp).WithOne(p => p.EmployeeKey)
                .HasForeignKey<EmployeeKey>(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLOYEE_KEYS");
        });

        

        modelBuilder.Entity<EmployeeShift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("SYS_C008397");

            entity.ToTable("EMPLOYEE_SHIFT");

            entity.Property(e => e.ShiftId)
                .HasColumnType("NUMBER")
                .HasColumnName("SHIFT_ID");
            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.EndTime)
                .HasPrecision(6)
                .HasColumnName("END_TIME");
            entity.Property(e => e.ShiftDate)
                .HasColumnType("DATE")
                .HasColumnName("SHIFT_DATE");
            entity.Property(e => e.StartTime)
                .HasPrecision(6)
                .HasColumnName("START_TIME");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValueSql("'SCHEDULED'")
                .HasColumnName("STATUS");

            entity.HasOne(d => d.Emp).WithMany(p => p.EmployeeShifts)
                .HasForeignKey(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHIFT_EMPLOYEE");
        });

        modelBuilder.Entity<OrderRequest>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("SYS_C008339");

            entity.ToTable("ORDER_REQUEST");

            entity.Property(e => e.OrderId)
                .HasColumnType("NUMBER")
                .HasColumnName("ORDER_ID");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CUSTOMER_PHONE");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.OrderType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ORDER_TYPE");
            entity.Property(e => e.ReceivedDate)
                .HasColumnType("DATE")
                .HasColumnName("RECEIVED_DATE");
            entity.Property(e => e.ReceiverEmp)
                .HasColumnType("NUMBER")
                .HasColumnName("RECEIVER_EMP");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");

            entity.HasOne(d => d.CustomerPhoneNavigation).WithMany(p => p.OrderRequests)
                .HasForeignKey(d => d.CustomerPhone)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORDER_CUSTOMER");

            entity.HasOne(d => d.ReceiverEmpNavigation).WithMany(p => p.OrderRequests)
                .HasForeignKey(d => d.ReceiverEmp)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORDER_EMPLOYEE");
        });

        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(e => e.PartId).HasName("SYS_C008361");

            entity.ToTable("PART");

            entity.HasIndex(e => e.Serial, "SYS_C008362").IsUnique();

            entity.Property(e => e.PartId)
                .HasColumnType("NUMBER")
                .HasColumnName("PART_ID");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MANUFACTURER");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.OrderId)
                .HasColumnType("NUMBER")
                .HasColumnName("ORDER_ID");
            entity.Property(e => e.Serial)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SERIAL");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");
            entity.Property(e => e.StockinItemId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKIN_ITEM_ID");

            entity.HasOne(d => d.Order).WithMany(p => p.Parts)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_PART_ORDER");

            entity.HasOne(d => d.StockinItem).WithMany(p => p.Parts)
                .HasForeignKey(d => d.StockinItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PART_STOCKINITEM");
        });

        modelBuilder.Entity<PartRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("SYS_C008380");

            entity.ToTable("PART_REQUEST");

            entity.Property(e => e.RequestId)
                .HasColumnType("NUMBER")
                .HasColumnName("REQUEST_ID");
            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.OrderId)
                .HasColumnType("NUMBER")
                .HasColumnName("ORDER_ID");
            entity.Property(e => e.RequestDate)
                .HasColumnType("DATE")
                .HasColumnName("REQUEST_DATE");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");

            entity.HasOne(d => d.Emp).WithMany(p => p.PartRequests)
                .HasForeignKey(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PARTREQUEST_EMPLOYEE");

            entity.HasOne(d => d.Order).WithMany(p => p.PartRequests)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PARTREQUEST_ORDER");
        });

        modelBuilder.Entity<PartRequestItem>(entity =>
        {
            entity.HasKey(e => e.RequestItemId).HasName("SYS_C008385");

            entity.ToTable("PART_REQUEST_ITEM");

            entity.Property(e => e.RequestItemId)
                .HasColumnType("NUMBER")
                .HasColumnName("REQUEST_ITEM_ID");
            entity.Property(e => e.PartId)
                .HasColumnType("NUMBER")
                .HasColumnName("PART_ID");
            entity.Property(e => e.RequestId)
                .HasColumnType("NUMBER")
                .HasColumnName("REQUEST_ID");

            entity.HasOne(d => d.Part).WithMany(p => p.PartRequestItems)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PARTREQUESTITEM_PART");

            entity.HasOne(d => d.Request).WithMany(p => p.PartRequestItems)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PARTREQUESTITEM_REQUEST");
        });

        modelBuilder.Entity<StockIn>(entity =>
        {
            entity.HasKey(e => e.StockinId).HasName("SYS_C008349");

            entity.ToTable("STOCK_IN");

            entity.Property(e => e.StockinId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKIN_ID");
            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.InDate)
                .HasColumnType("DATE")
                .HasColumnName("IN_DATE");
            entity.Property(e => e.Note)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("NOTE");

            entity.HasOne(d => d.Emp).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKIN_EMPLOYEE");
        });

        modelBuilder.Entity<StockInItem>(entity =>
        {
            entity.HasKey(e => e.StockinItemId).HasName("SYS_C008354");

            entity.ToTable("STOCK_IN_ITEM");

            entity.HasIndex(e => e.Serial, "SYS_C008355").IsUnique();

            entity.Property(e => e.StockinItemId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKIN_ITEM_ID");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MANUFACTURER");
            entity.Property(e => e.PartName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("PART_NAME");
            entity.Property(e => e.Serial)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SERIAL");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValueSql("'IN_STOCK'")
                .HasColumnName("STATUS");
            entity.Property(e => e.StockinId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKIN_ID");

            entity.HasOne(d => d.Stockin).WithMany(p => p.StockInItems)
                .HasForeignKey(d => d.StockinId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKINITEM_STOCKIN");
        });

        modelBuilder.Entity<StockOut>(entity =>
        {
            entity.HasKey(e => e.StockoutId).HasName("SYS_C008368");

            entity.ToTable("STOCK_OUT");

            entity.Property(e => e.StockoutId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKOUT_ID");
            entity.Property(e => e.EmpId)
                .HasColumnType("NUMBER")
                .HasColumnName("EMP_ID");
            entity.Property(e => e.Note)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("NOTE");
            entity.Property(e => e.OrderId)
                .HasColumnType("NUMBER")
                .HasColumnName("ORDER_ID");
            entity.Property(e => e.OutDate)
                .HasColumnType("DATE")
                .HasColumnName("OUT_DATE");

            entity.HasOne(d => d.Emp).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.EmpId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKOUT_EMPLOYEE");

            entity.HasOne(d => d.Order).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKOUT_ORDER");
        });

        modelBuilder.Entity<StockOutItem>(entity =>
        {
            entity.HasKey(e => e.StockoutItemId).HasName("SYS_C008373");

            entity.ToTable("STOCK_OUT_ITEM");

            entity.Property(e => e.StockoutItemId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKOUT_ITEM_ID");
            entity.Property(e => e.PartId)
                .HasColumnType("NUMBER")
                .HasColumnName("PART_ID");
            entity.Property(e => e.StockoutId)
                .HasColumnType("NUMBER")
                .HasColumnName("STOCKOUT_ID");

            entity.HasOne(d => d.Part).WithMany(p => p.StockOutItems)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKOUTITEM_PART");

            entity.HasOne(d => d.Stockout).WithMany(p => p.StockOutItems)
                .HasForeignKey(d => d.StockoutId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STOCKOUTITEM_STOCKOUT");
        });

        modelBuilder.Entity<UserOtpLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008392");

            entity.ToTable("USER_OTP_LOG");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(6)
                .HasDefaultValueSql("SYSTIMESTAMP")
                .HasColumnName("CREATED_AT");
            entity.Property(e => e.ExpiredAt)
                .HasPrecision(6)
                .HasColumnName("EXPIRED_AT");
            entity.Property(e => e.Otp)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("OTP");
            entity.Property(e => e.Used)
                .HasMaxLength(1)
                .IsUnicode(false)
                .HasDefaultValueSql("'N'\n")
                .IsFixedLength()
                .HasColumnName("USED");
            entity.Property(e => e.UserId)
                .HasColumnType("NUMBER")
                .HasColumnName("USER_ID");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("USERNAME");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
