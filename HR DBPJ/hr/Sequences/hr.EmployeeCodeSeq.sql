-- Numeric part of the next suggested employee code (EMP + at least 3 digits). Gaps are acceptable.
CREATE SEQUENCE [hr].[EmployeeCodeSeq]
    AS int
    START WITH 1
    INCREMENT BY 1
    MINVALUE 1
    NO CYCLE
    CACHE 10;
