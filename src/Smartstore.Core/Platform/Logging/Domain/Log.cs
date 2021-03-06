﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Smartstore.Core.Identity;
using Smartstore.Data.Caching;
using Smartstore.Data.Hooks;
using Smartstore.Domain;

namespace Smartstore.Core.Logging
{
    /// <summary>
    /// Represents a log level
    /// </summary>
    public enum LogLevel
    {
        Verbose = 0,
        Debug = 10,
        Information = 20,
        Warning = 30,
        Error = 40,
        Fatal = 50
    }

    /// <summary>
    /// Represents a log record
    /// </summary>
    [Index(nameof(Logger), Name = "IX_Log_Logger")]
    [Index(nameof(LogLevelId), Name = "IX_Log_Level")]
    [Index(nameof(CreatedOnUtc), Name = "IX_Log_CreatedOnUtc")]
    [Hookable(false)]
    [CacheableEntity(NeverCache = true)]
    public partial class Log : BaseEntity
    {
        private readonly ILazyLoader _lazyLoader;

        public Log()
        {
        }

        public Log(ILazyLoader lazyLoader)
        {
            _lazyLoader = lazyLoader;
        }

        /// <summary>
        /// Gets or sets the log level identifier
        /// </summary>
        public int LogLevelId { get; set; }

        /// <summary>
        /// Gets or sets the short message
        /// </summary>
        [Required, StringLength(4000)]
        public string ShortMessage { get; set; }

        /// <summary>
        /// Gets or sets the full exception
        /// </summary>
        [MaxLength]
        public string FullMessage { get; set; }

        /// <summary>
        /// Gets or sets the IP address
        /// </summary>
        [StringLength(200)]
        public string IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the customer identifier
        /// </summary>
        public int? CustomerId { get; set; }

        /// <summary>
        /// Gets or sets the page URL
        /// </summary>
        [StringLength(1500)]
        public string PageUrl { get; set; }

        /// <summary>
        /// Gets or sets the referrer URL
        /// </summary>
        [StringLength(1500)]
        public string ReferrerUrl { get; set; }

        /// <summary>
        /// Gets or sets the date and time of instance creation
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the logger name
        /// </summary>
        [Required, StringLength(400)]
        public string Logger { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method
        /// </summary>
        [StringLength(10)]
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        [StringLength(100)]
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the log level
        /// </summary>
        [NotMapped]
        public LogLevel LogLevel
        {
            get => (LogLevel)LogLevelId;
            set => LogLevelId = (int)value;
        }

        private Customer _customer;
        /// <summary>
        /// Gets or sets the customer
        /// </summary>
        public Customer Customer 
        {
            get => _lazyLoader?.Load(this, ref _customer) ?? _customer;
            set => _customer = value;
        }
    }
}
